using Application.CustoFixo.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WebApi;

public class CustoFixoLembreteBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CustoFixoLembreteBackgroundService> _logger;
    private static readonly TimeSpan IntervaloExecucao = TimeSpan.FromHours(1);

    public CustoFixoLembreteBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<CustoFixoLembreteBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield(); // Garantir que o método seja assíncrono desde o início 

        _logger.LogInformation("Background Service de Lembretes de Custos Fixos inicializado.");

        // Atraso estratégico para garantir que o Task.Run do MongoConfig (mapeamentos BsonClassMap) finalize
        // antes de realizar qualquer consulta, evitando Auto-Mapping indesejado com StringSerializer.
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var timezone = ObterTimeZoneSaoPaulo();
                var horaLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);

                // Executar apenas entre 8h e 17h (inclusive) no fuso horário de São Paulo
                if (horaLocal.Hour >= 8 && horaLocal.Hour <= 17)
                {
                    _logger.LogInformation("Dentro do horário de processamento (8h-17h SP). Hora local: {Hora}. Iniciando processador de lembretes.", horaLocal.ToString("HH:mm:ss"));

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var lembreteService = scope.ServiceProvider.GetRequiredService<ICustoFixoLembreteService>();
                        await lembreteService.ProcessarLembretesAsync();
                    }
                }
                else
                {
                    _logger.LogInformation("Fora do horário de processamento (8h-17h SP). Hora local: {Hora}. Aguardando próximo ciclo.", horaLocal.ToString("HH:mm:ss"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado na execução do CustoFixoLembreteBackgroundService.");
            }

            // Aguardar 1 hora
            await Task.Delay(IntervaloExecucao, stoppingToken);
        }
    }

    private TimeZoneInfo ObterTimeZoneSaoPaulo()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }
    }
}
