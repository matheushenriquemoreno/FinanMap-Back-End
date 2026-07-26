using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using OpenIddict.Abstractions;
using WebApi.Mcp;
using Xunit;

namespace Tests;

[Collection(McpMongoCollection.Name)]
public sealed class McpOAuthHostContractTests(McpMongoFixture mongo)
{
    [Fact]
    public async Task Real_openiddict_metadata_announces_interoperable_public_pkce_contract()
    {
        await using var fixture = await McpOAuthHostFixture.StartAsync(mongo.ConnectionString);

        using var response = await fixture.Client.GetAsync(
            "/.well-known/oauth-authorization-server");
        Assert.True(
            response.IsSuccessStatusCode,
            await response.Content.ReadAsStringAsync());
        using var metadata = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = metadata.RootElement;

        Assert.Equal(
            "https://api.example/oauth/register",
            root.GetProperty("registration_endpoint").GetString());
        Assert.Equal(
            ["S256"],
            root.GetProperty("code_challenge_methods_supported")
                .EnumerateArray().Select(item => item.GetString()!).ToArray());
        Assert.Equal(
            ["none"],
            root.GetProperty("token_endpoint_auth_methods_supported")
                .EnumerateArray().Select(item => item.GetString()!).ToArray());
    }

    [Fact]
    public async Task Real_dcr_accepts_rfc_snake_case_payload_and_creates_public_client()
    {
        await using var fixture = await McpOAuthHostFixture.StartAsync(mongo.ConnectionString);
        const string payload = """
            {
              "client_name": "Inspector Fixture",
              "redirect_uris": ["http://127.0.0.1:6274/oauth/callback"],
              "token_endpoint_auth_method": "none",
              "scope": "mcp:read mcp:audit"
            }
            """;

        using var response = await fixture.Client.PostAsync(
            "/oauth/register",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        Assert.True(
            response.IsSuccessStatusCode,
            await response.Content.ReadAsStringAsync());
        using var registration = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var clientId = registration.RootElement.GetProperty("client_id").GetString()!;
        Assert.StartsWith("mcp_", clientId);
        Assert.Equal(
            "none",
            registration.RootElement.GetProperty("token_endpoint_auth_method").GetString());
        await fixture.DeleteClientAsync(clientId);
    }

    private sealed class McpOAuthHostFixture : IAsyncDisposable
    {
        private readonly WebApplication _application;

        private McpOAuthHostFixture(
            WebApplication application,
            HttpClient client,
            IServiceProvider services)
        {
            _application = application;
            Client = client;
            Services = services;
        }

        public HttpClient Client { get; }
        private IServiceProvider Services { get; }

        public static async Task<McpOAuthHostFixture> StartAsync(string connectionString)
        {
            var databaseName = $"FinanMapMcpTests_{Guid.NewGuid():N}";
            var mongoClient = new MongoClient(connectionString);
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });
            builder.WebHost.UseTestServer();
            builder.Host.UseDefaultServiceProvider(options =>
                options.ValidateOnBuild = false);
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mcp:EndpointEnabled"] = "true",
                ["MCP_PUBLIC_BASE_URL"] = "https://api.example"
            });
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddAuthorization();
            builder.Services.AddSingleton<IMongoClient>(mongoClient);
            builder.Services.AddSingleton(mongoClient.GetDatabase(databaseName));
            builder.Services.AddMcpFinanceiro(builder.Configuration, builder.Environment);

            var application = builder.Build();
            application.UseAuthentication();
            application.UseAuthorization();
            application.MapMcpTransport();
            application.MapMcpOAuthEndpoints();
            await application.StartAsync();

            return new McpOAuthHostFixture(
                application,
                application.GetTestClient(),
                application.Services);
        }

        public async Task DeleteClientAsync(string clientId)
        {
            await using var scope = Services.CreateAsyncScope();
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var application = await manager.FindByClientIdAsync(clientId);
            if (application is not null)
                await manager.DeleteAsync(application);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _application.DisposeAsync();
        }
    }
}
