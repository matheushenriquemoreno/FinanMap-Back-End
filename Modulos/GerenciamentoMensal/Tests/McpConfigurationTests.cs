using Application.Mcp.Configuration;
using Application.Mcp.Interfaces;
using Application.Mcp.Services;
using Domain.Mcp.Repositories;
using Infra.Data.Mongo.Mcp;
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

    [Fact]
    public void Enabled_production_writes_require_preview_encryption_key()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mcp:WriteToolsEnabled"] = "true"
            })
            .Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddMcpFinanceiro(
                configuration,
                new TestHostEnvironment(Environments.Production)));

        Assert.Contains("MCP_PREVIEW_ENCRYPTION_KEY", exception.Message);
        Assert.Contains("base64", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("32 bytes", exception.Message);
    }

    [Theory]
    [InlineData("not-base64")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==")]
    public void Preview_encryption_key_must_be_base64_with_exactly_32_bytes(
        string encryptionKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mcp:WriteToolsEnabled"] = "true",
                ["MCP_PREVIEW_ENCRYPTION_KEY"] = encryptionKey
            })
            .Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddMcpFinanceiro(
                configuration,
                new TestHostEnvironment(Environments.Development)));

        Assert.Contains("MCP_PREVIEW_ENCRYPTION_KEY", exception.Message);
        Assert.Contains("base64", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("32 bytes", exception.Message);
    }

    [Fact]
    public void Enabled_writes_register_engine_dependencies_with_safe_lifetimes()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mcp:WriteToolsEnabled"] = "true",
                ["MCP_PREVIEW_ENCRYPTION_KEY"] =
                    Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray())
            })
            .Build();
        var services = new ServiceCollection();

        services.AddMcpFinanceiro(
            configuration,
            new TestHostEnvironment(Environments.Development));

        AssertScoped<IMcpWriteEffectStore, McpWriteEffectStore>(services);
        AssertScoped<IMcpPreviewRepository, McpPreviewRepository>(services);
        AssertScoped<IMcpConfirmationJournalRepository, McpOperationJournalRepository>(
            services);
        AssertScoped<IMcpWriteDomainGateway, McpWriteDomainGateway>(services);
        AssertScoped<McpWriteService, McpWriteService>(services);
        AssertScoped<McpOperationReconciler, McpOperationReconciler>(services);
        var worker = Assert.Single(
            services,
            item =>
                item.ServiceType == typeof(IHostedService) &&
                item.ImplementationType ==
                typeof(McpOperationReconciliationWorker));
        Assert.Equal(ServiceLifetime.Singleton, worker.Lifetime);
        var protector = Assert.Single(
            services,
            item => item.ServiceType == typeof(IMcpPreviewPayloadProtector));
        Assert.Equal(ServiceLifetime.Singleton, protector.Lifetime);
        var timeProvider = Assert.Single(
            services,
            item => item.ServiceType == typeof(TimeProvider));
        Assert.Equal(ServiceLifetime.Singleton, timeProvider.Lifetime);
        Assert.Same(TimeProvider.System, timeProvider.ImplementationInstance);
    }

    [Fact]
    public void Disabled_writes_do_not_register_write_engine_or_reconciliation_worker()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mcp:WriteToolsEnabled"] = "false"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddMcpFinanceiro(
            configuration,
            new TestHostEnvironment(Environments.Development));

        Assert.DoesNotContain(
            services,
            item => item.ServiceType == typeof(McpWriteService));
        Assert.DoesNotContain(
            services,
            item => item.ServiceType == typeof(McpOperationReconciler));
        Assert.DoesNotContain(
            services,
            item =>
                item.ServiceType == typeof(IHostedService) &&
                item.ImplementationType ==
                typeof(McpOperationReconciliationWorker));
    }

    private static void AssertScoped<TService, TImplementation>(
        IServiceCollection services)
    {
        var descriptor = Assert.Single(
            services,
            item => item.ServiceType == typeof(TService));

        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        if (descriptor.ImplementationType is not null)
        {
            Assert.Equal(typeof(TImplementation), descriptor.ImplementationType);
            return;
        }

        Assert.NotNull(descriptor.ImplementationFactory);
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
