using Application.Mcp.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using WebApi.Mcp;
using Xunit;

namespace Tests;

public sealed class McpHealthCheckPhase6Tests
{
    [Fact]
    public async Task Write_readiness_is_unhealthy_without_journal_and_reconciler()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IOptions<McpFeatureOptions>>(Options.Create(
            new McpFeatureOptions
            {
                EndpointEnabled = true,
                WriteToolsEnabled = true
            }));
        services.AddHealthChecks().AddCheck<McpHealthCheck>("mcp");
        await using var provider = services.BuildServiceProvider();

        var report = await provider
            .GetRequiredService<HealthCheckService>()
            .CheckHealthAsync();

        Assert.Equal(HealthStatus.Unhealthy, report.Status);
        Assert.Contains(
            "journal",
            report.Entries["mcp"].Description!,
            StringComparison.OrdinalIgnoreCase);
    }
}
