using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Application.Mcp.Configuration;
using Application.Mcp.Interfaces;
using Application.Mcp.Models;
using Application.Mcp.Services;
using Domain.Mcp.Repositories;
using Infra.Data.Mongo.Mcp;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace WebApi.Mcp;

public static class McpServiceCollectionExtensions
{
    public static IServiceCollection AddMcpFinanceiro(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var endpointEnabled = IsEndpointEnabled(configuration);
        var writeToolsEnabled = IsWriteToolsEnabled(configuration);
        var publicBaseUrl =
            configuration["MCP_PUBLIC_BASE_URL"] ??
            configuration[$"{McpFeatureOptions.SectionName}:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            if (endpointEnabled && !environment.IsDevelopment())
                throw new InvalidOperationException(
                    "MCP_PUBLIC_BASE_URL é obrigatória fora do ambiente Development.");
            publicBaseUrl = "https://localhost";
        }
        var mcpAudience = $"{publicBaseUrl.TrimEnd('/')}/mcp";

        services.AddSingleton<IConfigureOptions<McpFeatureOptions>, McpFeatureOptionsConfigurator>();
        services.AddSingleton<McpTelemetry>();
        services.AddScoped<McpConnectionService>();
        services.AddScoped<IMcpAuthorizationGrantStore, OpenIddictAuthorizationGrantStore>();
        services.AddScoped<IMcpConnectionValidator>(
            provider => provider.GetRequiredService<McpConnectionService>());
        services.AddScoped<McpCategoriesToolService>();
        services.AddScoped<IMcpFinancialReadSource, McpFinancialReadSource>();
        services.AddScoped<McpFinancialReadService>();
        var cursorKeySetting = configuration["MCP_CURSOR_SIGNING_KEY"];
        if (endpointEnabled &&
            !environment.IsDevelopment() &&
            string.IsNullOrWhiteSpace(cursorKeySetting))
        {
            throw new InvalidOperationException(
                "MCP_CURSOR_SIGNING_KEY é obrigatória fora do ambiente Development para manter cursores válidos entre reinícios e instâncias.");
        }
        var cursorKey = string.IsNullOrWhiteSpace(cursorKeySetting)
            ? RandomNumberGenerator.GetBytes(32)
            : SHA256.HashData(Encoding.UTF8.GetBytes(cursorKeySetting));
        services.AddSingleton(new McpCursorCodec(cursorKey));
        services.AddSingleton<McpAuditSanitizer>();
        if (writeToolsEnabled)
        {
            var previewEncryptionKey = ResolvePreviewEncryptionKey(
                configuration,
                environment);
            services.AddSingleton<IMcpPreviewPayloadProtector>(
                new McpPreviewPayloadProtector(previewEncryptionKey));
            services.AddSingleton(TimeProvider.System);
            services.AddScoped<IMcpWriteEffectStore, McpWriteEffectStore>();
            services.AddScoped<IMcpPreviewRepository, McpPreviewRepository>();
            services.AddScoped<IMcpImportRepository, McpImportRepository>();
            services.AddScoped<
                IMcpConfirmationJournalRepository,
                McpOperationJournalRepository>();
            services.AddScoped<IMcpWriteDomainGateway, McpWriteDomainGateway>();
            services.AddScoped<McpWriteService>();
            services.AddScoped<McpImportService>();
            services.AddScoped<IMcpImportService>(
                provider => provider.GetRequiredService<McpImportService>());
            services.AddScoped<IMcpImportCategoryResolver, McpImportCategoryResolver>();
            services.AddScoped<McpOperationReconciler>();
            services.AddHostedService<McpOperationReconciliationWorker>();
            services.AddHostedService<McpImportProcessingWorker>();
        }
        services.AddLogging(logging =>
            logging.AddFilter("OpenIddict", LogLevel.Warning));

        var mcpServer = services.AddMcpServer()
            .WithHttpTransport(options =>
            {
                options.Stateless = true;
            })
            .AddAuthorizationFilters()
            .WithRequestFilters(filters =>
                filters.AddCallToolFilter(next => async (request, cancellationToken) =>
                {
                    var telemetry = request.Services?
                        .GetRequiredService<McpTelemetry>() ??
                        throw new InvalidOperationException(
                            "Telemetria MCP indisponível.");
                    var startedAt = Stopwatch.GetTimestamp();
                    var operationClass = ToolOperationClass(request.Params?.Name);
                    telemetry.TrackConfirmationAttempt(
                        request.Params?.Name,
                        request.Params?.Arguments);
                    try
                    {
                        var result = await next(request, cancellationToken);
                        telemetry.RecordToolCall(
                            operationClass,
                            result.IsError == true ? "error" : "success",
                            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                        return result;
                    }
                    catch (McpJournalUnavailableException)
                    {
                        telemetry.RecordJournalWriteFailure();
                        telemetry.RecordToolCall(
                            operationClass,
                            "error",
                            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                        throw;
                    }
                    catch
                    {
                        telemetry.RecordToolCall(
                            operationClass,
                            "error",
                            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                        throw;
                    }
                }))
            .WithTools<McpCategoriesTool>()
            .WithTools<McpFinancialTools>();
        if (writeToolsEnabled)
        {
            var writeSerializerOptions =
                new JsonSerializerOptions(McpJsonUtilities.DefaultOptions)
                {
                    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
                };
            var schemaOptions = new AIJsonSchemaCreateOptions
            {
                TransformSchemaNode = static (_, schema) =>
                {
                    if (schema is JsonObject objectSchema &&
                        objectSchema["type"] is JsonValue typeNode &&
                        typeNode.TryGetValue<string>(out var type) &&
                        type == "object")
                    {
                        objectSchema["additionalProperties"] = false;
                    }

                    return schema;
                }
            };
            var writeTools = new[] { typeof(McpWriteTools), typeof(McpImportTools) }
                .SelectMany(toolType => toolType
                    .GetMethods()
                    .Where(method =>
                        method.GetCustomAttributes(
                                typeof(McpServerToolAttribute),
                                inherit: false)
                            .Length > 0)
                    .Select(method => (ToolType: toolType, Method: method)))
                .Select(definition =>
                {
                    var tool = McpServerTool.Create(
                        definition.Method,
                        request => ActivatorUtilities.CreateInstance(
                            request.Services ??
                            throw new InvalidOperationException(
                                "Escopo de serviços MCP indisponível."),
                            definition.ToolType),
                        new McpServerToolCreateOptions
                        {
                            SerializerOptions = writeSerializerOptions,
                            SchemaCreateOptions = schemaOptions
                        });
                    var inputSchema = JsonNode.Parse(
                            tool.ProtocolTool.InputSchema.GetRawText())
                        ?.AsObject() ??
                        throw new InvalidOperationException(
                            $"Schema MCP inválido para {tool.ProtocolTool.Name}.");
                    inputSchema["additionalProperties"] = false;
                    tool.ProtocolTool.InputSchema =
                        JsonSerializer.SerializeToElement(inputSchema);
                    return tool;
                });
            mcpServer.WithTools(writeTools);
        }

        var openIddict = services.AddOpenIddict();
        openIddict.AddCore(options => options.UseMongoDb());
        openIddict.AddServer(options =>
        {
            options.SetIssuer(new Uri(publicBaseUrl))
                .SetConfigurationEndpointUris("/.well-known/oauth-authorization-server")
                .SetAuthorizationEndpointUris("/oauth/authorize")
                .SetTokenEndpointUris("/oauth/token")
                .SetRevocationEndpointUris("/oauth/revoke")
                .AllowAuthorizationCodeFlow()
                .AllowRefreshTokenFlow()
                .RequireProofKeyForCodeExchange()
                .RegisterScopes("mcp:read", "mcp:write", "mcp:import", "mcp:audit")
                .RegisterResources(mcpAudience)
                .SetAccessTokenLifetime(TimeSpan.FromMinutes(10))
                .SetRefreshTokenLifetime(TimeSpan.FromDays(30));

            options.Configure(serverOptions =>
            {
                serverOptions.ClientAuthenticationMethods.Clear();
                serverOptions.ClientAuthenticationMethods.Add(ClientAuthenticationMethods.None);
                serverOptions.CodeChallengeMethods.Clear();
                serverOptions.CodeChallengeMethods.Add(CodeChallengeMethods.Sha256);
            });
            options.AddEventHandler<ApplyConfigurationResponseContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Response.SetParameter(
                        "registration_endpoint",
                        $"{publicBaseUrl.TrimEnd('/')}/oauth/register");
                    context.Response.SetParameter(
                        "code_challenge_methods_supported",
                        JsonSerializer.SerializeToElement(
                            new[] { CodeChallengeMethods.Sha256 }));
                    context.Response.SetParameter(
                        "token_endpoint_auth_methods_supported",
                        JsonSerializer.SerializeToElement(
                            new[] { ClientAuthenticationMethods.None }));
                    return default;
                }));
            options.AddEventHandler<ProcessErrorContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    if (context.EndpointType ==
                        OpenIddict.Server.OpenIddictServerEndpointType.Token)
                    {
                        context.Logger.LogWarning(
                            "OAuth token request rejected with error {OAuthError}; " +
                            "request and response payloads omitted.",
                            context.Error ?? "unknown");
                    }
                    return default;
                }));

            var signingPath = configuration["MCP_SIGNING_CERTIFICATE_PATH"];
            var encryptionPath = configuration["MCP_ENCRYPTION_CERTIFICATE_PATH"];
            if (string.IsNullOrWhiteSpace(signingPath) ||
                string.IsNullOrWhiteSpace(encryptionPath))
            {
                if (endpointEnabled && !environment.IsDevelopment())
                {
                    throw new InvalidOperationException(
                        "Certificados persistentes MCP de assinatura e criptografia são obrigatórios fora de Development.");
                }

                options.AddDevelopmentEncryptionCertificate()
                    .AddDevelopmentSigningCertificate();
            }
            else
            {
                var signing = X509CertificateLoader.LoadPkcs12FromFile(
                    signingPath, configuration["MCP_SIGNING_CERTIFICATE_PASSWORD"]);
                var encryption = X509CertificateLoader.LoadPkcs12FromFile(
                    encryptionPath, configuration["MCP_ENCRYPTION_CERTIFICATE_PASSWORD"]);
                options.AddSigningCertificate(signing)
                    .AddEncryptionCertificate(encryption);
            }

            var server = options.UseAspNetCore()
                .EnableAuthorizationEndpointPassthrough()
                .EnableStatusCodePagesIntegration();
            if (environment.IsDevelopment())
                server.DisableTransportSecurityRequirement();
        });
        openIddict.AddValidation(options =>
        {
            options.UseLocalServer();
            options.AddAudiences(mcpAudience);
            options.UseAspNetCore();
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("McpBearer", policy =>
            {
                policy.AddAuthenticationSchemes(
                    OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
            });
        });
        services.AddRateLimiter(options =>
        {
            options.AddPolicy("mcp-dcr", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
            options.AddPolicy("mcp-transport", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ResolveRateLimitPartition(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 120,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (rejected, cancellationToken) =>
            {
                rejected.HttpContext.Response.Headers.RetryAfter = "60";
                rejected.HttpContext.RequestServices
                    .GetRequiredService<McpTelemetry>()
                    .RecordRateLimited();
                await rejected.HttpContext.Response.WriteAsJsonAsync(
                    new
                    {
                        code = "LIMIT_EXCEEDED",
                        message = "O limite de solicitações MCP foi atingido.",
                        guidance = "Aguarde antes de tentar novamente e divida lotes grandes em solicitações menores."
                    },
                    cancellationToken);
            };
        });
        var allowedOrigins =
            (Environment.GetEnvironmentVariable("MCP_ALLOWED_ORIGINS") ??
             configuration["MCP_ALLOWED_ORIGINS"] ??
             configuration[$"{McpFeatureOptions.SectionName}:AllowedOrigins"])
            ?.Split(
                [';', ','],
                StringSplitOptions.TrimEntries |
                StringSplitOptions.RemoveEmptyEntries) ?? [];
        services.AddCors(options =>
        {
            options.AddPolicy("mcp", policy =>
            {
                if (allowedOrigins.Length > 0)
                    policy.WithOrigins(allowedOrigins);
                policy.AllowAnyHeader().AllowAnyMethod();
            });
        });

        services.AddHealthChecks().AddCheck<McpHealthCheck>("mcp");
        return services;
    }

    private static string ResolveRateLimitPartition(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.FirstOrDefault();
        var source = string.IsNullOrWhiteSpace(authorization)
            ? context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            : authorization;
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(source)))[..24];
    }

    private static string ToolOperationClass(string? toolName)
    {
        if (toolName?.StartsWith(
                "finanmap_import_",
                StringComparison.Ordinal) == true)
        {
            return "import";
        }
        if (toolName?.Contains(
                "_preview",
                StringComparison.Ordinal) == true ||
            toolName?.StartsWith(
                "finanmap_operation_",
                StringComparison.Ordinal) == true)
        {
            return "write";
        }
        return "read";
    }

    private static bool IsEndpointEnabled(IConfiguration configuration)
    {
        var rawValue =
            Environment.GetEnvironmentVariable("MCP_FEATURE_ENABLED") ??
            configuration["MCP_FEATURE_ENABLED"] ??
            configuration[$"{McpFeatureOptions.SectionName}:EndpointEnabled"];
        return bool.TryParse(rawValue, out var enabled) && enabled;
    }

    private static bool IsWriteToolsEnabled(IConfiguration configuration)
    {
        var rawValue =
            Environment.GetEnvironmentVariable("MCP_WRITE_TOOLS_ENABLED") ??
            configuration["MCP_WRITE_TOOLS_ENABLED"] ??
            configuration[$"{McpFeatureOptions.SectionName}:WriteToolsEnabled"];
        return bool.TryParse(rawValue, out var enabled) && enabled;
    }

    private static byte[] ResolvePreviewEncryptionKey(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        const string settingName = "MCP_PREVIEW_ENCRYPTION_KEY";
        var configured =
            Environment.GetEnvironmentVariable(settingName) ??
            configuration[settingName];
        if (string.IsNullOrWhiteSpace(configured))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    $"{settingName} é obrigatória fora do ambiente Development e deve conter exatamente 32 bytes em base64.");
            }

            return RandomNumberGenerator.GetBytes(32);
        }

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(configured.Trim());
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                $"{settingName} deve conter exatamente 32 bytes em base64.",
                exception);
        }

        if (decoded.Length != 32)
        {
            throw new InvalidOperationException(
                $"{settingName} deve conter exatamente 32 bytes em base64.");
        }

        return decoded;
    }
}
