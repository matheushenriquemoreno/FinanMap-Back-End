using Application.Mcp.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace WebApi.Mcp;

public sealed class McpHealthCheck(IOptions<McpFeatureOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var feature = options.Value;
        var data = new Dictionary<string, object>
        {
            ["protocolRevision"] = McpProtocolContract.Revision,
            ["sdkVersion"] = McpProtocolContract.SdkVersion,
            ["endpointEnabled"] = feature.EndpointEnabled,
            ["writeToolsEnabled"] = feature.WriteToolsEnabled,
            ["historyEnabled"] = feature.HistoryEnabled
        };

        return Task.FromResult(HealthCheckResult.Healthy(
            feature.EndpointEnabled
                ? "Endpoint MCP de leitura habilitado."
                : "Endpoint MCP desabilitado por feature flag.",
            data));
    }
}
