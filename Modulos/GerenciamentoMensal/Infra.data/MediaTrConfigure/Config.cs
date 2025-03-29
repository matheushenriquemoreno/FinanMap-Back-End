using System.Reflection;
using Infra.MediaTrConfigure.Publisher;
using Infra.MediaTrConfigure.Queue;
using Microsoft.Extensions.DependencyInjection;

namespace Infra.MediaTrConfigure;

public static class Config
{
    public static IServiceCollection ConfigureMediaTR(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.Load("Application"));
            cfg.NotificationPublisherType = typeof(ChannelPublisher);
        });

        services.AddSingleton<NotificationsQueue>();
        services.AddHostedService<ChannelPublisherWorker>();
        return services;
    }
}
