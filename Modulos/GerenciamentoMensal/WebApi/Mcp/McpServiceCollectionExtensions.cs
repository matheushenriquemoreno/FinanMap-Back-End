using System.Text.Json;
using Application.Mcp.Configuration;
using Application.Mcp.Interfaces;
using Application.Mcp.Services;
using Microsoft.Extensions.Options;
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
        services.AddScoped<McpConnectionService>();
        services.AddScoped<IMcpAuthorizationGrantStore, OpenIddictAuthorizationGrantStore>();
        services.AddScoped<IMcpConnectionValidator>(
            provider => provider.GetRequiredService<McpConnectionService>());
        services.AddScoped<McpCategoriesToolService>();
        services.AddSingleton<McpAuditSanitizer>();
        services.AddLogging(logging =>
            logging.AddFilter("OpenIddict", LogLevel.Warning));

        services.AddMcpServer()
            .WithHttpTransport(options =>
            {
                options.Stateless = true;
            })
            .AddAuthorizationFilters()
            .WithTools<McpCategoriesTool>();

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
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        services.AddHealthChecks().AddCheck<McpHealthCheck>("mcp");
        return services;
    }

    private static bool IsEndpointEnabled(IConfiguration configuration)
    {
        var rawValue =
            Environment.GetEnvironmentVariable("MCP_FEATURE_ENABLED") ??
            configuration["MCP_FEATURE_ENABLED"] ??
            configuration[$"{McpFeatureOptions.SectionName}:EndpointEnabled"];
        return bool.TryParse(rawValue, out var enabled) && enabled;
    }
}
