using Infra.MediaTrConfigure.Queue;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infra.MediaTrConfigure.Publisher
{
    public class ChannelPublisherWorker(NotificationsQueue queue, ILogger<ChannelPublisherWorker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Le as mensagens publicadas na fila e inicia o processamento
            await foreach (NotificationEntry entry in queue.Reader.ReadAllAsync(stoppingToken))
            {

                await Parallel.ForEachAsync(entry.Handlers, stoppingToken,
                    async (executor, token) =>
                {
                    logger.LogInformation("Notificação sendo publicada, {0}", entry.Notification.GetType().Name);
                    await executor.HandlerCallback(entry.Notification, token);
                    logger.LogInformation("Processamento da notificação foi realizado com sucesso, {0}", entry.Notification.GetType().Name);
                });
            }
        }
    }
}
