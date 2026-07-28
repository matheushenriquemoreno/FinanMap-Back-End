using Application.Mcp.Configuration;
using Application.Mcp.Services;
using Domain.Mcp.Repositories;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace WebApi.Mcp;

public sealed class McpHealthCheck(
    IOptions<McpFeatureOptions> options,
    IServiceScopeFactory scopeFactory) : IHealthCheck
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
        if (feature.WriteToolsEnabled)
        {
            using var scope = scopeFactory.CreateScope();
            var services = scope.ServiceProvider;
            var journalReady =
                services.GetService<IMcpConfirmationJournalRepository>() is not null;
            var reconcilerReady =
                services.GetService<McpOperationReconciler>() is not null;
            var previewProtectionReady =
                services.GetService<IMcpPreviewPayloadProtector>() is not null;
            data["journalReady"] = journalReady;
            data["reconcilerReady"] = reconcilerReady;
            data["previewProtectionReady"] = previewProtectionReady;
            if (!journalReady || !reconcilerReady || !previewProtectionReady)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "Escritas MCP sem prontidão de journal, reconciliador ou proteção de prévia.",
                    data: data));
            }
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            feature.EndpointEnabled
                ? "Endpoint MCP de leitura habilitado."
                : "Endpoint MCP desabilitado por feature flag.",
            data));
    }
}
