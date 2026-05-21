using Application.CustoFixo.Interfaces;
using Domain.Entity;
using Domain.Enum;
using Domain.Repository;
using Domain.Compartilhamento.Repository;
using Microsoft.Extensions.Logging;
using Application.Email.Interfaces;
using Application.Email.DTOs;
using System.Linq;

namespace Application.CustoFixo.Service;

public class CustoFixoLembreteService : ICustoFixoLembreteService
{
    private readonly ICustoFixoRepository _custoFixoRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly ICompartilhamentoRepository _compartilhamentoRepository;
    private readonly ICustoFixoLembreteHistoricoRepository _historicoRepository;
    private readonly ICustoFixoEmailService _custoFixoEmailService;
    private readonly ILogger<CustoFixoLembreteService> _logger;

    public CustoFixoLembreteService(
        ICustoFixoRepository custoFixoRepository,
        IUsuarioRepository usuarioRepository,
        ICompartilhamentoRepository compartilhamentoRepository,
        ICustoFixoLembreteHistoricoRepository historicoRepository,
        ICustoFixoEmailService custoFixoEmailService,
        ILogger<CustoFixoLembreteService> logger)
    {
        _custoFixoRepository = custoFixoRepository;
        _usuarioRepository = usuarioRepository;
        _compartilhamentoRepository = compartilhamentoRepository;
        _historicoRepository = historicoRepository;
        _custoFixoEmailService = custoFixoEmailService;
        _logger = logger;
    }

    public async Task ProcessarLembretesAsync()
    {
        var timezone = ObterTimeZoneSaoPaulo();
        var dataLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);

        _logger.LogInformation("Iniciando processamento de lembretes de custos fixos. Hora local de referência: {HoraLocal}", dataLocal.ToString("yyyy-MM-dd HH:mm:ss"));

        // 1. Processar lembretes para "Hoje" (Dia do Vencimento)
        await ProcessarParaDataReferencia(dataLocal, TipoLembrete.DiaDoVencimento);

        // 2. Processar lembretes para "Hoje + 3 dias" (Antecedência)
        var dataAntecedencia = dataLocal.AddDays(3);
        await ProcessarParaDataReferencia(dataAntecedencia, TipoLembrete.Antecedencia);
    }

    private async Task ProcessarParaDataReferencia(DateTime dataReferencia, TipoLembrete tipo)
    {
        int diaVencimentoBuscado = dataReferencia.Day;
        bool ehUltimoDiaDoMes = dataReferencia.Day == DateTime.DaysInMonth(dataReferencia.Year, dataReferencia.Month);

        var custosCandidatos = new List<Domain.Entity.CustoFixo>();

        if (ehUltimoDiaDoMes)
        {
            // Se for o último dia do mês, além do dia buscado, pegamos qualquer dia de vencimento maior (29, 30, 31)
            for (int dia = diaVencimentoBuscado; dia <= 31; dia++)
            {
                var custosDia = await _custoFixoRepository.GetCustosFixosAtivosPorDiaVencimento(dia);
                custosCandidatos.AddRange(custosDia);
            }
        }
        else
        {
            var custosDia = await _custoFixoRepository.GetCustosFixosAtivosPorDiaVencimento(diaVencimentoBuscado);
            custosCandidatos.AddRange(custosDia);
        }

        if (custosCandidatos == null || !custosCandidatos.Any())
        {
            _logger.LogInformation("Nenhum custo fixo ativo encontrado com vencimento no dia {Dia} (Tipo: {Tipo})", diaVencimentoBuscado, tipo);
            return;
        }

        var custosAgrupadosPorUsuario = custosCandidatos.GroupBy(x => x.UsuarioId);

        foreach (var grupo in custosAgrupadosPorUsuario)
        {
            var usuarioId = grupo.Key;
            var custosDoUsuario = grupo.ToList();

            var usuario = await _usuarioRepository.GetById(usuarioId);
            if (usuario == null)
            {
                _logger.LogWarning("Usuário {UsuarioId} dono de custos fixos não foi encontrado no banco de dados.", usuarioId);
                continue;
            }

            if (!usuario.ReceberNotificacoesCustosFixos)
            {
                _logger.LogInformation("Usuário {Nome} ({UsuarioId}) possui opt-out global ativo. Ignorando envio de lembretes.", usuario.Nome, usuarioId);
                continue;
            }

            var compartilhamentosComoConvidado = await _compartilhamentoRepository.ObterPorConvidadoId(usuarioId);
            bool ehConvidadoAtivo = compartilhamentosComoConvidado != null &&
                                    compartilhamentosComoConvidado.Any(c => c.Status == Domain.Compartilhamento.Entity.StatusConvite.Aceito);

            if (ehConvidadoAtivo)
            {
                _logger.LogInformation("Usuário {Nome} ({UsuarioId}) é um convidado em uma conta compartilhada. Ignorando envio de lembretes.", usuario.Nome, usuarioId);
                continue;
            }

            bool jaEnviado = await _historicoRepository.ExisteRegistroAsync(usuarioId, dataReferencia.Date, tipo);
            if (jaEnviado)
            {
                _logger.LogInformation("Lembrete do tipo {Tipo} para a data de vencimento {DataVencimento} já foi enviado para o usuário {Nome} ({UsuarioId}). Pulando.", tipo, dataReferencia.ToString("yyyy-MM-dd"), usuario.Nome, usuarioId);
                continue;
            }

            // Envio de e-mail real
            int diasRestantes = tipo == TipoLembrete.DiaDoVencimento ? 0 : 3;
            var itensEmail = custosDoUsuario
                .Select(c => new CustoFixoLembreteItem(c.Nome, diasRestantes))
                .ToList();

            _logger.LogInformation("Enviando lembrete de custo fixo real ({Tipo}) para {Email} ({Nome}). Vencimento: {DataVencimento}.",
                tipo, usuario.Email, usuario.Nome, dataReferencia.ToString("yyyy-MM-dd"));

            var resultadoEnvio = await _custoFixoEmailService.EnviarLembreteAsync(usuario.Email, usuario.Nome, itensEmail, tipo);

            if (resultadoEnvio.IsSucess)
            {
                _logger.LogInformation("Lembrete do tipo {Tipo} enviado com sucesso para {Email}.", tipo, usuario.Email);

                // Registrar idempotência apenas em caso de sucesso no envio do e-mail
                var historico = new CustoFixoLembreteHistorico(usuarioId, dataReferencia.Date, tipo);
                try
                {
                    await _historicoRepository.RegistrarEnvioAsync(historico);
                    _logger.LogInformation("Registrada idempotência de lembrete do tipo {Tipo} na data {DataVencimento} para o usuário {UsuarioId}.", tipo, dataReferencia.ToString("yyyy-MM-dd"), usuarioId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao registrar idempotência para o usuário {UsuarioId} na data {DataVencimento}. Possível duplicidade de thread prevenida.", usuarioId, dataReferencia.ToString("yyyy-MM-dd"));
                }
            }
            else
            {
                _logger.LogError("Falha ao enviar e-mail de lembrete do tipo {Tipo} para {Email}. Erro: {ErroMessage}. Idempotência NÃO registrada para permitir retry na próxima execução.",
                    tipo, usuario.Email, resultadoEnvio.Error?.Message);
            }
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
