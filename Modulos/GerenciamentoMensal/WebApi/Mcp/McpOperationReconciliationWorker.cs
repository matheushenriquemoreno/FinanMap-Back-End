using Application.Mcp.Services;

namespace WebApi.Mcp;

public sealed class McpOperationReconciliationWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<McpOperationReconciliationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await ReconcileAsync(stoppingToken);
    }

    private async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var reconciler = scope.ServiceProvider
                .GetRequiredService<McpOperationReconciler>();
            var result = await reconciler.ReconcileDueAsync(
                BatchSize,
                cancellationToken);
            if (result.Scanned > 0)
            {
                logger.LogInformation(
                    "Reconciliação MCP processou {Scanned} operações: {Completed} concluídas, {Rejected} rejeitadas, {Unknown} desconhecidas e {Skipped} ignoradas.",
                    result.Scanned,
                    result.Completed,
                    result.Rejected,
                    result.Unknown,
                    result.Skipped);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Falha ao executar o ciclo de reconciliação MCP.");
        }
    }
}
