using Application.Mcp.Services;

namespace WebApi.Mcp;

public sealed class McpImportProcessingWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<McpImportProcessingWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);
    private const int BatchSize = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ProcessAsync(stoppingToken);
        using var timer = new PeriodicTimer(Interval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await ProcessAsync(stoppingToken);
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<McpImportService>();
            var processed = await service.ProcessDueBatchesAsync(
                BatchSize,
                cancellationToken);
            if (processed > 0)
            {
                logger.LogInformation(
                    "Worker MCP concluiu {Processed} lotes de importação; payload omitido.",
                    processed);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Falha no worker MCP de importação; lotes permanecem duráveis para retomada após expiração do lease.");
        }
    }
}
