using Application.Mcp.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using WebApi.Mcp;
using Xunit;

namespace Tests;

public class McpConfigurationTests
{
    [Fact]
    public void Protocol_contract_is_pinned_to_approved_stable_versions()
    {
        Assert.Equal("2025-11-25", McpProtocolContract.Revision);
        Assert.Equal("1.4.1", McpProtocolContract.SdkVersion);
        Assert.Equal("authorization_code", McpProtocolContract.OAuthGrant);
        Assert.Equal("S256", McpProtocolContract.PkceMethod);
        Assert.Equal(new[] { "mcp:read", "mcp:audit" }, McpProtocolContract.ReadOnlyScopes);
    }

    [Fact]
    public void Feature_flags_are_independent_and_disabled_by_default()
    {
        var options = new McpFeatureOptions();

        Assert.False(options.EndpointEnabled);
        Assert.False(options.WriteToolsEnabled);
        Assert.False(options.HistoryEnabled);
    }

    [Fact]
    public void Disabled_endpoint_does_not_require_production_oauth_secrets()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mcp:EndpointEnabled"] = "false"
            })
            .Build();
        var services = new ServiceCollection();

        var exception = Record.Exception(() =>
            services.AddMcpFinanceiro(
                configuration,
                new TestHostEnvironment(Environments.Production)));

        Assert.Null(exception);
    }

    [Fact]
    public void Enabled_production_endpoint_requires_stable_cursor_signing_key()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MCP_FEATURE_ENABLED"] = "true",
                ["MCP_PUBLIC_BASE_URL"] = "https://api.example.test"
            })
            .Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddMcpFinanceiro(
                configuration,
                new TestHostEnvironment(Environments.Production)));

        Assert.Contains("MCP_CURSOR_SIGNING_KEY", exception.Message);
        Assert.Contains("reinícios e instâncias", exception.Message);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
