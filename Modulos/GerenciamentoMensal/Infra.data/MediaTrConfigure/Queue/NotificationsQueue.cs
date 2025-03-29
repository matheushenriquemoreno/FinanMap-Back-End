using System.Threading.Channels;
using MediatR;

namespace Infra.MediaTrConfigure.Queue;

// É o canal que gerencia a passagem real da notificação, que em tempo de memoria vai ficar armazenada aqui 
// Até que algum serviço em segundo plano leia a mensagem e publique a ação da Notificação do media TR
public class NotificationsQueue(int capacidade = 100)
{
    private readonly Channel<NotificationEntry> _queue =
        Channel.CreateBounded<NotificationEntry>(new BoundedChannelOptions(capacidade)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    public ChannelReader<NotificationEntry> Reader => _queue.Reader;
    public ChannelWriter<NotificationEntry> Writer => _queue.Writer;
}

public record NotificationEntry(NotificationHandlerExecutor[] Handlers, INotification Notification);
