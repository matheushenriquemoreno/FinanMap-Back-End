using Infra.MediaTrConfigure.Queue;
using MediatR;

namespace Infra.MediaTrConfigure.Publisher;

internal class ChannelPublisher(NotificationsQueue queue) : INotificationPublisher
{
    public async Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
    {
        // Escreve a mensagem no canal e retorna imediatamente
        await queue.Writer.WriteAsync(
            new NotificationEntry(handlerExecutors.ToArray(), notification),
            cancellationToken);
    }
}
