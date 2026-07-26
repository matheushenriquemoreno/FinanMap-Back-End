using System.Diagnostics;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Application.DTOs;
using Application.Interfaces;
using Domain.Compartilhamento.Entity;
using Domain.Entity;
using Domain.Enum;
using Domain.Login.Interfaces;
using Domain.Mcp.Repositories;
using Infra.Data.Mongo.Mcp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SharedDomain;
using WebApi.Mcp;
using Xunit;

namespace Tests;

[Collection(McpMongoCollection.Name)]
public sealed class McpAuthenticatedInspectorSmokeTests(McpMongoFixture mongo)
{
    [Fact]
    public async Task Inspector_cli_calls_tool_with_ephemeral_oauth_fixture()
    {
        var port = AllocateLoopbackPort();
        var baseUrl = $"http://127.0.0.1:{port}";
        var ownerId = $"inspector-owner-{Guid.NewGuid():N}";
        var logs = new OAuthLogCanary();
        await using var host = await StartHostAsync(baseUrl, ownerId);
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(baseUrl)
        };

        var registration = await RegisterClientAsync(client);
        await AssertDenialContinuationAsync(client, registration.ClientId, baseUrl);
        var verifier = $"mcp-code-verifier-sentinel-{new string('A', 64)}";
        var challenge = Microsoft.AspNetCore.WebUtilities.Base64UrlTextEncoder.Encode(
            SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var redirectUri = "http://127.0.0.1:6274/oauth/callback";
        var state = Microsoft.AspNetCore.WebUtilities.Base64UrlTextEncoder.Encode(
            RandomNumberGenerator.GetBytes(24));
        var authorizeUrl = QueryHelpers.AddQueryString(
            "/oauth/authorize",
            new Dictionary<string, string?>
            {
                ["client_id"] = registration.ClientId,
                ["redirect_uri"] = redirectUri,
                ["response_type"] = "code",
                ["scope"] = "mcp:read mcp:audit",
                ["state"] = state,
                ["code_challenge"] = challenge,
                ["code_challenge_method"] = "S256",
                ["resource"] = $"{baseUrl}/mcp"
            });

        using var consentRedirect = await client.GetAsync(authorizeUrl);
        Assert.True(
            consentRedirect.StatusCode == HttpStatusCode.Redirect,
            $"{consentRedirect.StatusCode}: {await consentRedirect.Content.ReadAsStringAsync()}");
        var consentQuery = QueryHelpers.ParseQuery(consentRedirect.Headers.Location!.Query);
        var interactionId = consentQuery["mcpAuthorizationInteraction"].Single()!;

        using var interactionResponse = await client.GetAsync(
            $"/api/mcp/authorization-interactions/{interactionId}");
        interactionResponse.EnsureSuccessStatusCode();
        using var interactionJson = JsonDocument.Parse(
            await interactionResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            redirectUri,
            interactionJson.RootElement.GetProperty("redirectUri").GetString());
        Assert.Equal(
            state,
            interactionJson.RootElement.GetProperty("state").GetString());
        foreach (var forbiddenProperty in new[]
        {
            "codeChallenge",
            "codeChallengeMethod",
            "codeVerifier",
            "accessToken",
            "refreshToken",
            "clientSecret"
        })
        {
            Assert.False(interactionJson.RootElement.TryGetProperty(
                forbiddenProperty, out _));
        }

        using var approval = await client.PostAsJsonAsync(
            $"/api/mcp/authorization-interactions/{interactionId}/approve",
            new { scopes = new[] { "mcp:read", "mcp:audit" } });
        approval.EnsureSuccessStatusCode();
        var continuation = approval.Headers
            .GetValues("X-Mcp-Authorization-Continue")
            .Single();

        using var callback = await client.GetAsync(continuation);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        var callbackQuery = QueryHelpers.ParseQuery(callback.Headers.Location!.Query);
        Assert.Equal(state, callbackQuery["state"].Single());
        var code = callbackQuery["code"].Single()!;

        using var tokenResponse = await client.PostAsync(
            "/oauth/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = registration.ClientId,
                ["redirect_uri"] = redirectUri,
                ["code"] = code,
                ["code_verifier"] = verifier,
                ["resource"] = $"{baseUrl}/mcp"
            }));
        tokenResponse.EnsureSuccessStatusCode();
        using var tokenJson = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
        var accessToken = tokenJson.RootElement.GetProperty("access_token").GetString()!;
        var refreshToken = tokenJson.RootElement.TryGetProperty("refresh_token", out var refreshTokenElement)
            ? refreshTokenElement.GetString()
            : null;
        const string refreshTokenSentinel = "mcp-refresh-token-sentinel";
        const string clientSecretSentinel = "mcp-client-secret-sentinel";

        using var invalidTokenResponse = await client.PostAsync(
            "/oauth/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = registration.ClientId,
                ["refresh_token"] = refreshTokenSentinel,
                ["client_secret"] = clientSecretSentinel
            }));
        Assert.True(
            invalidTokenResponse.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized);

        var result = await RunInspectorAsync($"{baseUrl}/mcp", accessToken);

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains("Categoria fixture", result.Output);
        Assert.Contains("success", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            !logs.ContainsAny(
                verifier,
                code,
                accessToken,
                refreshToken,
                refreshTokenSentinel,
                clientSecretSentinel),
            "O canário detectou material OAuth secreto em logs habilitados.");
        Assert.True(
            logs.ContainsOpenIddictWarningOrError(),
            "Warnings/errors úteis do OpenIddict devem continuar habilitados.");

        async Task<WebApplication> StartHostAsync(string publicBaseUrl, string fixtureOwner)
        {
            var mongoClient = new MongoClient(mongo.ConnectionString);
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });
            builder.WebHost.UseKestrel().UseUrls(publicBaseUrl);
            builder.Logging.ClearProviders();
            builder.Logging.SetMinimumLevel(LogLevel.Trace);
            builder.Logging.AddProvider(logs);
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mcp:EndpointEnabled"] = "true",
                ["Mcp:HistoryEnabled"] = "true",
                ["MCP_PUBLIC_BASE_URL"] = publicBaseUrl,
                ["MCP_CONSENT_URL"] = "http://frontend.fixture/mcp-consent"
            });
            builder.Services.AddHttpContextAccessor();
            builder.Services
                .AddAuthentication(FixtureAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, FixtureAuthenticationHandler>(
                    FixtureAuthenticationHandler.SchemeName, _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddRateLimiter(_ => { });
            builder.Services.AddSingleton<IMongoClient>(mongoClient);
            builder.Services.AddSingleton(
                mongoClient.GetDatabase($"FinanMapInspector_{Guid.NewGuid():N}"));
            builder.Services.AddScoped<IMcpConnectionRepository, McpConnectionRepository>();
            builder.Services.AddScoped<IMcpAuthorizationInteractionRepository, McpAuthorizationInteractionRepository>();
            builder.Services.AddScoped<McpOperationJournalRepository>();
            builder.Services.AddScoped<IMcpOperationJournalRepository>(
                provider => provider.GetRequiredService<McpOperationJournalRepository>());
            builder.Services.AddScoped<IMcpAuditQueryRepository>(
                provider => provider.GetRequiredService<McpOperationJournalRepository>());
            builder.Services.AddSingleton<IUsuarioLogado>(new FixtureUsuarioLogado(fixtureOwner));
            builder.Services.AddScoped<ICategoriaService, FixtureCategoriaService>();
            builder.Services.AddMcpFinanceiro(builder.Configuration, builder.Environment);

            var application = builder.Build();
            application.UseRateLimiter();
            application.UseMcpTransportSecurity();
            application.UseAuthentication();
            application.UseAuthorization();
            application.MapMcpTransport();
            application.MapMcpOAuthEndpoints();
            application.MapMcpOAuthAuthorizeEndpoint();
            application.MapMcpApiEndpoints();
            await application.StartAsync();
            return application;
        }
    }

    private static async Task AssertDenialContinuationAsync(
        HttpClient client,
        string clientId,
        string baseUrl)
    {
        var redirectUri = "http://127.0.0.1:6274/oauth/callback";
        var state = Microsoft.AspNetCore.WebUtilities.Base64UrlTextEncoder.Encode(
            RandomNumberGenerator.GetBytes(24));
        var verifier = Microsoft.AspNetCore.WebUtilities.Base64UrlTextEncoder.Encode(
            RandomNumberGenerator.GetBytes(32));
        var challenge = Microsoft.AspNetCore.WebUtilities.Base64UrlTextEncoder.Encode(
            SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var authorizeUrl = QueryHelpers.AddQueryString(
            "/oauth/authorize",
            new Dictionary<string, string?>
            {
                ["client_id"] = clientId,
                ["redirect_uri"] = redirectUri,
                ["response_type"] = "code",
                ["scope"] = "mcp:read",
                ["state"] = state,
                ["code_challenge"] = challenge,
                ["code_challenge_method"] = "S256",
                ["resource"] = $"{baseUrl}/mcp"
            });

        using var consentRedirect = await client.GetAsync(authorizeUrl);
        Assert.Equal(HttpStatusCode.Redirect, consentRedirect.StatusCode);
        var consentQuery = QueryHelpers.ParseQuery(consentRedirect.Headers.Location!.Query);
        var interactionId = consentQuery["mcpAuthorizationInteraction"].Single()!;

        using var interactionResponse = await client.GetAsync(
            $"/api/mcp/authorization-interactions/{interactionId}");
        interactionResponse.EnsureSuccessStatusCode();

        using var denial = await client.PostAsJsonAsync(
            $"/api/mcp/authorization-interactions/{interactionId}/deny",
            new { reasonCode = "fixture_denied" });
        Assert.Equal(HttpStatusCode.NoContent, denial.StatusCode);
        var continuation = denial.Headers
            .GetValues("X-Mcp-Authorization-Continue")
            .Single();
        var continuationUri = new Uri(continuation);
        var continuationQuery = QueryHelpers.ParseQuery(continuationUri.Query);
        Assert.Equal(redirectUri, continuationUri.GetLeftPart(UriPartial.Path));
        Assert.Equal("access_denied", continuationQuery["error"].Single());
        Assert.Equal(state, continuationQuery["state"].Single());
        Assert.False(continuationQuery.ContainsKey("code"));
        Assert.False(continuationQuery.ContainsKey("access_token"));
        Assert.False(continuationQuery.ContainsKey("code_challenge"));
    }

    private static async Task<(string ClientId, string ClientName)> RegisterClientAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/oauth/register", new
        {
            client_name = "Inspector Smoke Fixture",
            redirect_uris = new[] { "http://127.0.0.1:6274/oauth/callback" },
            token_endpoint_auth_method = "none",
            scope = "mcp:read mcp:audit"
        });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (
            json.RootElement.GetProperty("client_id").GetString()!,
            json.RootElement.GetProperty("client_name").GetString()!);
    }

    private static async Task<(int ExitCode, string Output)> RunInspectorAsync(
        string endpoint,
        string accessToken)
    {
        var nodeRoot = OperatingSystem.IsWindows()
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "nodejs")
            : "/usr/local";
        var nodeExecutable = OperatingSystem.IsWindows()
            ? Path.Combine(nodeRoot, "node.exe")
            : Path.Combine(nodeRoot, "bin", "node");
        var npxCli = OperatingSystem.IsWindows()
            ? Path.Combine(nodeRoot, "node_modules", "npm", "bin", "npx-cli.js")
            : Path.Combine(nodeRoot, "lib", "node_modules", "npm", "bin", "npx-cli.js");
        var startInfo = new ProcessStartInfo
        {
            FileName = nodeExecutable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(npxCli);
        foreach (var argument in new[]
        {
            "-y",
            "@modelcontextprotocol/inspector@0.21.2",
            "--cli",
            endpoint,
            "--transport",
            "http",
            "--method",
            "tools/call",
            "--tool-name",
            "finanmap_categories_list",
            "--header",
            $"Authorization: Bearer {accessToken}"
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Não foi possível iniciar o MCP Inspector.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);
        var output = $"{await outputTask}\n{await errorTask}"
            .Replace(accessToken, "[REDACTED]", StringComparison.Ordinal);
        return (process.ExitCode, output);
    }

    private static int AllocateLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class FixtureAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "McpFixture";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
                [new Claim("Id", "fixture-authenticated")],
                SchemeName);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }

    private sealed class OAuthLogCanary : ILoggerProvider
    {
        private readonly ConcurrentQueue<OAuthLogEntry> _entries = new();

        public ILogger CreateLogger(string categoryName) =>
            new OAuthCanaryLogger(categoryName, _entries);

        public void Dispose()
        {
        }

        public bool ContainsAny(params string?[] sensitiveValues)
        {
            var values = sensitiveValues
                .Where(value => !string.IsNullOrEmpty(value))
                .Cast<string>()
                .ToArray();
            return _entries.Any(entry =>
                values.Any(value => entry.Payload.Contains(value, StringComparison.Ordinal)));
        }

        public bool ContainsOpenIddictWarningOrError() =>
            _entries.Any(entry =>
                entry.Level >= LogLevel.Warning &&
                entry.Category.StartsWith("OpenIddict", StringComparison.Ordinal));

        private sealed class OAuthCanaryLogger(
            string category,
            ConcurrentQueue<OAuthLogEntry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var rendered = formatter(state, exception);
                var structured = state is IEnumerable<KeyValuePair<string, object?>> values
                    ? string.Join(
                        Environment.NewLine,
                        values.Select(value => $"{value.Key}={value.Value}"))
                    : string.Empty;
                entries.Enqueue(new OAuthLogEntry(
                    category,
                    logLevel,
                    $"{rendered}{Environment.NewLine}{structured}"));
            }
        }

        private sealed record OAuthLogEntry(
            string Category,
            LogLevel Level,
            string Payload);
    }

    private sealed class FixtureUsuarioLogado(string ownerId) : IUsuarioLogado
    {
        public string Id => ownerId;
        public Usuario Usuario => throw new NotSupportedException();
        public string IdContextoDados => ownerId;
        public Usuario UsuarioContextoDados => throw new NotSupportedException();
        public bool EmModoCompartilhado => false;
        public NivelPermissao? PermissaoAtual => null;
    }

    private sealed class FixtureCategoriaService : ICategoriaService
    {
        public Task<Result<List<ResultCategoriaDTO>>> ObterCategoria(
            TipoCategoria tipoCategoria,
            string descricao)
        {
            var result = tipoCategoria == TipoCategoria.Despesa
                ? new List<ResultCategoriaDTO>
                {
                    new()
                    {
                        Id = "fixture-category",
                        Nome = "Categoria fixture",
                        Tipo = TipoCategoria.Despesa
                    }
                }
                : [];
            return Task.FromResult(Result.Success(result));
        }

        public Task<Result<ResultCategoriaDTO>> Adicionar(CreateCategoriaDTO createDTO) =>
            throw new NotSupportedException();
        public Task<Result<ResultCategoriaDTO>> Atualizar(UpdateCategoriaDTO updateDTO) =>
            throw new NotSupportedException();
        public Task<Result> Excluir(string id) => throw new NotSupportedException();
        public Task<Result<ResultCategoriaDTO>> ObterPeloID(string id) =>
            throw new NotSupportedException();
    }
}
