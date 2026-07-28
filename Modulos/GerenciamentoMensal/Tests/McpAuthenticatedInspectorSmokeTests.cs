using System.Diagnostics;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Application.DTOs;
using Application.Interfaces;
using Application.Mcp.Interfaces;
using Application.Mcp.Models;
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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
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
        var toolCalls = new FixtureToolCallTracker();
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

        var result = await RunInspectorMatrixAsync(
            $"{baseUrl}/mcp", accessToken);

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains("Categoria fixture", result.Output);
        Assert.Contains("success", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("categories", toolCalls.Domains);
        Assert.Contains("incomes", toolCalls.Domains);
        Assert.Contains("expenses", toolCalls.Domains);
        Assert.Contains("investments", toolCalls.Domains);
        Assert.Contains("fixed-costs", toolCalls.Domains);
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
            builder.Services.AddSingleton(toolCalls);
            builder.Services.AddScoped<ICategoriaService, FixtureCategoriaService>();
            builder.Services.AddMcpFinanceiro(builder.Configuration, builder.Environment);
            builder.Services.RemoveAll<IMcpFinancialReadSource>();
            builder.Services.AddScoped<
                IMcpFinancialReadSource,
                FixtureFinancialReadSource>();

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

    [Fact]
    public async Task Inspector_cli_proves_filtered_content_empty_state_and_oauth_owner_isolation_a_b()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var ownerA = $"OWNER_A_MARKER_{suffix}";
        var ownerB = $"OWNER_B_MARKER_{suffix}";
        var baseUrlA = $"http://127.0.0.1:{AllocateLoopbackPort()}";
        var baseUrlB = $"http://127.0.0.1:{AllocateLoopbackPort()}";
        var logs = new OAuthLogCanary();
        var calls = new FixtureToolCallTracker();
        await using var hostA = await StartOwnerHostAsync(
            mongo.ConnectionString, baseUrlA, ownerA, logs, calls);
        await using var hostB = await StartOwnerHostAsync(
            mongo.ConnectionString, baseUrlB, ownerB, logs, calls);
        using var clientA = new HttpClient(
            new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(baseUrlA)
        };
        using var clientB = new HttpClient(
            new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(baseUrlB)
        };

        var oauthA = await AuthorizeOwnerAsync(clientA, baseUrlA, "A");
        var oauthB = await AuthorizeOwnerAsync(clientB, baseUrlB, "B");
        var matrixA = await RunOwnerInspectorMatrixAsync(
            $"{baseUrlA}/mcp", oauthA.AccessToken, ownerA);
        var matrixB = await RunOwnerInspectorMatrixAsync(
            $"{baseUrlB}/mcp", oauthB.AccessToken, ownerB);

        AssertOwnerMatrix(matrixA, ownerA, ownerB);
        AssertOwnerMatrix(matrixB, ownerB, ownerA);
        foreach (var owner in new[] { ownerA, ownerB })
            AssertTrackedOwnerMatrix(calls.Calls, owner);
        Assert.True(
            !logs.ContainsAny(
                oauthA.Verifier,
                oauthA.Code,
                oauthA.AccessToken,
                oauthA.RefreshToken,
                oauthB.Verifier,
                oauthB.Code,
                oauthB.AccessToken,
                oauthB.RefreshToken),
            "O canário detectou material OAuth secreto da matriz A/B em logs habilitados.");
    }

    [Fact]
    public async Task Official_dotnet_sdk_and_inspector_cli_are_two_independent_authenticated_clients()
    {
        var baseUrl = $"http://127.0.0.1:{AllocateLoopbackPort()}";
        var ownerId = $"two-clients-owner-{Guid.NewGuid():N}";
        var logs = new OAuthLogCanary();
        var calls = new FixtureToolCallTracker();
        await using var host = await StartOwnerHostAsync(
            mongo.ConnectionString, baseUrl, ownerId, logs, calls);
        using var oauthClient = new HttpClient(
            new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(baseUrl)
        };
        var oauth = await AuthorizeOwnerAsync(oauthClient, baseUrl, "two-clients");

        using var sdkHttpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };
        sdkHttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", oauth.AccessToken);
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri($"{baseUrl}/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp
            },
            sdkHttpClient,
            loggerFactory: null,
            ownsHttpClient: false);
        await using var sdkClient = await McpClient.CreateAsync(
            transport,
            new McpClientOptions { ProtocolVersion = "2025-11-25" });

        var sdkResult = await sdkClient.CallToolAsync(
            "finanmap_categories_list",
            new Dictionary<string, object?>());
        var inspectorResult = await RunInspectorAsync(
            $"{baseUrl}/mcp",
            oauth.AccessToken,
            "finanmap_categories_list",
            new Dictionary<string, string>());

        Assert.NotEqual(true, sdkResult.IsError);
        Assert.Contains(
            "Categoria fixture",
            JsonSerializer.Serialize(sdkResult));
        Assert.True(inspectorResult.ExitCode == 0, inspectorResult.Output);
        Assert.Contains("Categoria fixture", inspectorResult.Output);
        Assert.True(
            calls.Calls.Count(
                call => call.UserId == ownerId && call.Domain == "categories") >= 2,
            "Os dois clientes devem alcançar a ferramenta autenticada.");
    }

    [Fact]
    public async Task Issued_bearer_cannot_call_tool_immediately_after_http_connection_revocation()
    {
        var baseUrl = $"http://127.0.0.1:{AllocateLoopbackPort()}";
        var ownerId = $"revocation-owner-{Guid.NewGuid():N}";
        var logs = new OAuthLogCanary();
        var calls = new FixtureToolCallTracker();
        await using var host = await StartOwnerHostAsync(
            mongo.ConnectionString, baseUrl, ownerId, logs, calls);
        using var client = new HttpClient(
            new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(baseUrl)
        };
        var oauth = await AuthorizeOwnerAsync(client, baseUrl, "revocation");

        using var connectionsResponse = await client.GetAsync("/api/mcp/connections");
        connectionsResponse.EnsureSuccessStatusCode();
        using var connectionsJson = JsonDocument.Parse(
            await connectionsResponse.Content.ReadAsStringAsync());
        var connectionId = connectionsJson.RootElement
            .GetProperty("items")[0]
            .GetProperty("id")
            .GetString()!;

        using var validRequest = CreateToolCallRequest(oauth.AccessToken);
        using var validResponse = await client.SendAsync(validRequest);
        Assert.NotEqual(HttpStatusCode.Unauthorized, validResponse.StatusCode);
        var callsBeforeRevocation = calls.Calls.Count;

        using var revokeResponse = await client.PostAsJsonAsync(
            $"/api/mcp/connections/{connectionId}/revoke",
            new { reasonCode = "security_test" });
        Assert.True(
            revokeResponse.IsSuccessStatusCode,
            await revokeResponse.Content.ReadAsStringAsync());

        using var revokedRequest = CreateToolCallRequest(oauth.AccessToken);
        using var revokedResponse = await client.SendAsync(revokedRequest);
        var revokedBody = await revokedResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, revokedResponse.StatusCode);
        Assert.Contains("\"status\":\"rejected\"", revokedBody);
        Assert.Contains("AUTH_CONNECTION_INACTIVE", revokedBody);
        Assert.Equal(callsBeforeRevocation, calls.Calls.Count);
    }

    private static HttpRequestMessage CreateToolCallRequest(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "tools/call",
                @params = new
                {
                    name = "finanmap_categories_list",
                    arguments = new { }
                }
            })
        };
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        return request;
    }

    private static async Task<WebApplication> StartOwnerHostAsync(
        string mongoConnectionString,
        string publicBaseUrl,
        string ownerId,
        OAuthLogCanary logs,
        FixtureToolCallTracker calls)
    {
        var mongoClient = new MongoClient(mongoConnectionString);
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
        builder.Services.AddScoped<
            IMcpAuthorizationInteractionRepository,
            McpAuthorizationInteractionRepository>();
        builder.Services.AddScoped<McpOperationJournalRepository>();
        builder.Services.AddScoped<IMcpOperationJournalRepository>(
            provider => provider.GetRequiredService<McpOperationJournalRepository>());
        builder.Services.AddScoped<IMcpAuditQueryRepository>(
            provider => provider.GetRequiredService<McpOperationJournalRepository>());
        builder.Services.AddSingleton<IUsuarioLogado>(
            new FixtureUsuarioLogado(ownerId));
        builder.Services.AddSingleton(calls);
        builder.Services.AddScoped<ICategoriaService, FixtureCategoriaService>();
        builder.Services.AddMcpFinanceiro(builder.Configuration, builder.Environment);
        builder.Services.RemoveAll<IMcpFinancialReadSource>();
        builder.Services.AddScoped<
            IMcpFinancialReadSource,
            FixtureFinancialReadSource>();

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

    private static async Task<OAuthFixtureSession> AuthorizeOwnerAsync(
        HttpClient client,
        string baseUrl,
        string label)
    {
        var registration = await RegisterClientAsync(client);
        var verifier =
            $"mcp-code-verifier-{label}-{Microsoft.AspNetCore.WebUtilities.Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(48))}";
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
        Assert.Equal(HttpStatusCode.Redirect, consentRedirect.StatusCode);
        var consentQuery = QueryHelpers.ParseQuery(
            consentRedirect.Headers.Location!.Query);
        var interactionId = consentQuery["mcpAuthorizationInteraction"].Single()!;
        using var approval = await client.PostAsJsonAsync(
            $"/api/mcp/authorization-interactions/{interactionId}/approve",
            new { scopes = new[] { "mcp:read", "mcp:audit" } });
        approval.EnsureSuccessStatusCode();
        var continuation = approval.Headers
            .GetValues("X-Mcp-Authorization-Continue")
            .Single();
        using var callback = await client.GetAsync(continuation);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        var callbackQuery = QueryHelpers.ParseQuery(
            callback.Headers.Location!.Query);
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
        using var tokenJson = JsonDocument.Parse(
            await tokenResponse.Content.ReadAsStringAsync());
        var accessToken = tokenJson.RootElement
            .GetProperty("access_token")
            .GetString()!;
        var refreshToken = tokenJson.RootElement.TryGetProperty(
            "refresh_token", out var refreshTokenElement)
            ? refreshTokenElement.GetString()
            : null;
        return new OAuthFixtureSession(
            verifier, code, accessToken, refreshToken);
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

    private static async Task<(int ExitCode, string Output)> RunInspectorMatrixAsync(
        string endpoint,
        string accessToken)
    {
        var calls = new[]
        {
            new InspectorToolCall(
                "finanmap_categories_list",
                new Dictionary<string, string>()),
            new InspectorToolCall(
                "finanmap_incomes_list",
                new Dictionary<string, string>
                {
                    ["from"] = "2026-01",
                    ["to"] = "2026-01"
                }),
            new InspectorToolCall(
                "finanmap_expenses_list",
                new Dictionary<string, string>
                {
                    ["from"] = "2026-01",
                    ["to"] = "2026-01"
                }),
            new InspectorToolCall(
                "finanmap_investments_list",
                new Dictionary<string, string>
                {
                    ["from"] = "2026-01",
                    ["to"] = "2026-01"
                }),
            new InspectorToolCall(
                "finanmap_fixed_costs_list",
                new Dictionary<string, string>())
        };
        var output = new StringBuilder();
        foreach (var call in calls)
        {
            var result = await RunInspectorAsync(
                endpoint,
                accessToken,
                call.ToolName,
                call.Arguments);
            output.AppendLine($"[{call.ToolName}]");
            output.AppendLine(result.Output);
            if (result.ExitCode != 0)
                return (result.ExitCode, output.ToString());
        }

        return (0, output.ToString());
    }

    private static async Task<OwnerInspectorMatrix> RunOwnerInspectorMatrixAsync(
        string endpoint,
        string accessToken,
        string ownerMarker)
    {
        var calls = new[]
        {
            new InspectorToolCall(
                "categories",
                "finanmap_categories_list",
                new Dictionary<string, string>
                {
                    ["tipo"] = "Despesa",
                    ["text"] = "selected",
                    ["limit"] = "1"
                }),
            new InspectorToolCall(
                "incomes",
                "finanmap_incomes_list",
                TransactionArguments(ownerMarker, "income", "2026-01")),
            new InspectorToolCall(
                "expenses",
                "finanmap_expenses_list",
                TransactionArguments(ownerMarker, "expense", "2026-01")),
            new InspectorToolCall(
                "investments",
                "finanmap_investments_list",
                TransactionArguments(ownerMarker, "investment", "2026-01")),
            new InspectorToolCall(
                "fixed-costs",
                "finanmap_fixed_costs_list",
                new Dictionary<string, string>
                {
                    ["active"] = "true",
                    ["category"] = $"{ownerMarker}-fixed-category",
                    ["limit"] = "1"
                }),
            new InspectorToolCall(
                "empty",
                "finanmap_incomes_list",
                TransactionArguments(ownerMarker, "income", "2026-02"))
        };
        var outputs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var call in calls)
        {
            var result = await RunInspectorAsync(
                endpoint,
                accessToken,
                call.ToolName,
                call.Arguments);
            outputs[call.Label] = result.Output;
            if (result.ExitCode != 0)
            {
                return new OwnerInspectorMatrix(
                    result.ExitCode,
                    outputs);
            }
        }

        return new OwnerInspectorMatrix(0, outputs);
    }

    private static Dictionary<string, string> TransactionArguments(
        string ownerMarker,
        string domain,
        string month) =>
        new()
        {
            ["from"] = month,
            ["to"] = month,
            ["category"] = $"{ownerMarker}-{domain}-category",
            ["description"] = "selected",
            ["limit"] = "1"
        };

    private static void AssertOwnerMatrix(
        OwnerInspectorMatrix matrix,
        string ownerMarker,
        string otherOwnerMarker)
    {
        Assert.True(
            matrix.ExitCode == 0,
            string.Join(
                Environment.NewLine,
                matrix.Outputs.Select(output =>
                    $"[{output.Key}]{Environment.NewLine}{output.Value}")));
        foreach (var domain in new[]
        {
            "categories",
            "incomes",
            "expenses",
            "investments",
            "fixed-costs"
        })
        {
            var output = matrix.Outputs[domain];
            Assert.Contains($"{ownerMarker}-{domain}-match", output);
            Assert.DoesNotContain($"{ownerMarker}-{domain}-other", output);
            Assert.DoesNotContain(otherOwnerMarker, output);
            Assert.Contains("success", output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("page", output, StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotContain(
            $"{ownerMarker}-categories-type-decoy",
            matrix.Outputs["categories"]);
        Assert.DoesNotContain(
            $"{ownerMarker}-categories-text-decoy",
            matrix.Outputs["categories"]);
        Assert.DoesNotContain(
            $"{ownerMarker}-fixed-costs-active-decoy",
            matrix.Outputs["fixed-costs"]);
        Assert.DoesNotContain(
            $"{ownerMarker}-fixed-costs-category-decoy",
            matrix.Outputs["fixed-costs"]);
        foreach (var domain in new[] { "incomes", "expenses", "investments" })
        {
            Assert.DoesNotContain(
                $"{ownerMarker}-{domain}-category-decoy",
                matrix.Outputs[domain]);
            Assert.DoesNotContain(
                $"{ownerMarker}-{domain}-description-decoy",
                matrix.Outputs[domain]);
            Assert.Contains(
                "BRL",
                matrix.Outputs[domain],
                StringComparison.Ordinal);
        }
        var empty = matrix.Outputs["empty"];
        Assert.Contains("empty", empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0.00", empty, StringComparison.Ordinal);
        Assert.Contains(
            "\"count\": 0",
            empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(otherOwnerMarker, empty);
    }

    private static void AssertTrackedOwnerMatrix(
        IReadOnlyCollection<FixtureSourceCall> calls,
        string ownerMarker)
    {
        Assert.Equal(6, calls.Count(call => call.UserId == ownerMarker));
        Assert.Contains(
            calls,
            call => call.UserId == ownerMarker &&
                    call.Domain == "categories" &&
                    call.Kind is null &&
                    call.From is null &&
                    call.To is null &&
                    call.CategoryType == TipoCategoria.Despesa &&
                    call.Description == "selected");
        Assert.Contains(
            calls,
            call => call.UserId == ownerMarker &&
                    call.Domain == "incomes" &&
                    call.Kind == McpFinancialKind.Income &&
                    call.From == new DateOnly(2026, 1, 1) &&
                    call.To == new DateOnly(2026, 1, 31));
        Assert.Contains(
            calls,
            call => call.UserId == ownerMarker &&
                    call.Domain == "expenses" &&
                    call.Kind == McpFinancialKind.Expense &&
                    call.From == new DateOnly(2026, 1, 1) &&
                    call.To == new DateOnly(2026, 1, 31));
        Assert.Contains(
            calls,
            call => call.UserId == ownerMarker &&
                    call.Domain == "investments" &&
                    call.Kind == McpFinancialKind.Investment &&
                    call.From == new DateOnly(2026, 1, 1) &&
                    call.To == new DateOnly(2026, 1, 31));
        Assert.Contains(
            calls,
            call => call.UserId == ownerMarker &&
                    call.Domain == "fixed-costs" &&
                    call.Kind is null &&
                    call.From is null &&
                    call.To is null);
        Assert.Contains(
            calls,
            call => call.UserId == ownerMarker &&
                    call.Domain == "incomes" &&
                    call.Kind == McpFinancialKind.Income &&
                    call.From == new DateOnly(2026, 2, 1) &&
                    call.To == new DateOnly(2026, 2, 28));
    }

    private static async Task<(int ExitCode, string Output)> RunInspectorAsync(
        string endpoint,
        string accessToken,
        string toolName,
        IReadOnlyDictionary<string, string> toolArguments)
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
            toolName
        })
        {
            startInfo.ArgumentList.Add(argument);
        }
        foreach (var argument in toolArguments)
        {
            startInfo.ArgumentList.Add("--tool-arg");
            startInfo.ArgumentList.Add($"{argument.Key}={argument.Value}");
        }
        startInfo.ArgumentList.Add("--header");
        startInfo.ArgumentList.Add($"Authorization: Bearer {accessToken}");

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

    private sealed record InspectorToolCall(
        string Label,
        string ToolName,
        IReadOnlyDictionary<string, string> Arguments)
    {
        public InspectorToolCall(
            string toolName,
            IReadOnlyDictionary<string, string> arguments)
            : this(toolName, toolName, arguments)
        {
        }
    }

    private sealed record OwnerInspectorMatrix(
        int ExitCode,
        IReadOnlyDictionary<string, string> Outputs);

    private sealed record OAuthFixtureSession(
        string Verifier,
        string Code,
        string AccessToken,
        string? RefreshToken);

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

    private sealed class FixtureCategoriaService(
        FixtureToolCallTracker calls,
        IUsuarioLogado usuarioLogado)
        : ICategoriaService
    {
        public Task<Result<List<ResultCategoriaDTO>>> ObterCategoria(
            TipoCategoria tipoCategoria,
            string descricao)
        {
            calls.Record(new FixtureSourceCall(
                usuarioLogado.Id,
                "categories",
                null,
                null,
                null,
                tipoCategoria,
                descricao));

            var ownerMarker = usuarioLogado.Id;
            if (!ownerMarker.StartsWith("OWNER_", StringComparison.Ordinal))
            {
                var legacyResult = tipoCategoria == TipoCategoria.Despesa
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
                return Task.FromResult(Result.Success(legacyResult));
            }

            if (tipoCategoria != TipoCategoria.Despesa)
            {
                return Task.FromResult(Result.Success(new List<ResultCategoriaDTO>
                {
                    new()
                    {
                        Id = $"{ownerMarker}-categories-type-decoy",
                        Nome = $"000-{ownerMarker}-categories-type-decoy-selected",
                        Tipo = tipoCategoria
                    }
                }));
            }

            if (!string.Equals(descricao, "selected", StringComparison.Ordinal))
            {
                return Task.FromResult(Result.Success(new List<ResultCategoriaDTO>
                {
                    new()
                    {
                        Id = $"{ownerMarker}-categories-text-decoy",
                        Nome = $"000-{ownerMarker}-categories-text-decoy",
                        Tipo = TipoCategoria.Despesa
                    }
                }));
            }

            return Task.FromResult(Result.Success(new List<ResultCategoriaDTO>
            {
                new()
                {
                    Id = $"{ownerMarker}-categories-match",
                    Nome = $"{ownerMarker}-categories-match-selected",
                    Tipo = TipoCategoria.Despesa
                }
            }));
        }

        public Task<Result<ResultCategoriaDTO>> Adicionar(CreateCategoriaDTO createDTO) =>
            throw new NotSupportedException();
        public Task<Result<ResultCategoriaDTO>> Atualizar(UpdateCategoriaDTO updateDTO) =>
            throw new NotSupportedException();
        public Task<Result> Excluir(string id) => throw new NotSupportedException();
        public Task<Result<ResultCategoriaDTO>> ObterPeloID(string id) =>
            throw new NotSupportedException();
    }

    private sealed class FixtureFinancialReadSource(FixtureToolCallTracker calls)
        : IMcpFinancialReadSource
    {
        public Task<IReadOnlyList<McpFinancialSourceRecord>> GetTransactionsAsync(
            string userId,
            McpFinancialKind kind,
            DateOnly from,
            DateOnly to,
            CancellationToken cancellationToken = default)
        {
            var domain = kind switch
            {
                McpFinancialKind.Income => "incomes",
                McpFinancialKind.Expense => "expenses",
                McpFinancialKind.Investment => "investments",
                _ => "unknown"
            };
            calls.Record(new FixtureSourceCall(
                userId,
                domain,
                kind,
                from,
                to));

            if (from.Year != 2026 || from.Month != 1 ||
                to.Year != 2026 || to.Month != 1)
            {
                return Task.FromResult<IReadOnlyList<McpFinancialSourceRecord>>([]);
            }

            return Task.FromResult<IReadOnlyList<McpFinancialSourceRecord>>(
            [
                new(
                    $"{userId}-{domain}-category-decoy",
                    kind,
                    2026,
                    1,
                    $"{userId}-{domain}-category-decoy-selected",
                    $"{userId}-{domain[..^1]}-other-category",
                    $"{userId}-{domain[..^1]}-other-category",
                    999.99m),
                new(
                    $"{userId}-{domain}-description-decoy",
                    kind,
                    2026,
                    1,
                    $"{userId}-{domain}-description-decoy",
                    $"{userId}-{domain[..^1]}-category",
                    $"{userId}-{domain[..^1]}-category",
                    888.88m),
                new(
                    $"{userId}-{domain}-match",
                    kind,
                    2026,
                    1,
                    $"{userId}-{domain}-match-selected",
                    $"{userId}-{domain[..^1]}-category",
                    $"{userId}-{domain[..^1]}-category",
                    123.45m)
            ]);
        }

        public Task<IReadOnlyList<McpFixedCostSourceRecord>> GetFixedCostsAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            calls.Record(new FixtureSourceCall(
                userId,
                "fixed-costs",
                null,
                null,
                null));
            return Task.FromResult<IReadOnlyList<McpFixedCostSourceRecord>>(
            [
                new(
                    $"{userId}-fixed-costs-active-decoy",
                    $"{userId}-fixed-costs-active-decoy",
                    1,
                    $"{userId}-fixed-category",
                    $"{userId}-fixed-category",
                    false),
                new(
                    $"{userId}-fixed-costs-category-decoy",
                    $"{userId}-fixed-costs-category-decoy",
                    2,
                    $"{userId}-fixed-other-category",
                    $"{userId}-fixed-other-category",
                    true),
                new(
                    $"{userId}-fixed-costs-match",
                    $"{userId}-fixed-costs-match",
                    10,
                    $"{userId}-fixed-category",
                    $"{userId}-fixed-category",
                    true)
            ]);
        }
    }

    private sealed class FixtureToolCallTracker
    {
        private readonly ConcurrentQueue<FixtureSourceCall> _calls = new();

        public IReadOnlyCollection<string> Domains =>
            _calls.Select(call => call.Domain).ToArray();

        public IReadOnlyCollection<FixtureSourceCall> Calls => _calls.ToArray();

        public void Record(string domain) =>
            _calls.Enqueue(new FixtureSourceCall(
                string.Empty, domain, null, null, null));

        public void Record(FixtureSourceCall call) => _calls.Enqueue(call);
    }

    private sealed record FixtureSourceCall(
        string UserId,
        string Domain,
        McpFinancialKind? Kind,
        DateOnly? From,
        DateOnly? To,
        TipoCategoria? CategoryType = null,
        string? Description = null);
}
