using Application.CustoFixo.Interfaces;
using Domain.Entity;
using Domain.Enum;
using Domain.Repository;
using Microsoft.Extensions.Logging;
using Application.Email.Interfaces;
using Application.Email.DTOs;
using System.Linq;

namespace Application.CustoFixo.Service;

public class CustoFixoLembreteService : ICustoFixoLembreteService
{
    private readonly ICustoFixoRepository _custoFixoRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly ICustoFixoLembreteHistoricoRepository _historicoRepository;
    private readonly ICustoFixoEmailService _custoFixoEmailService;
    private readonly ILogger<CustoFixoLembreteService> _logger;

    public CustoFixoLembreteService(
        ICustoFixoRepository custoFixoRepository,
        IUsuarioRepository usuarioRepository,
        ICustoFixoLembreteHistoricoRepository historicoRepository,
        ICustoFixoEmailService custoFixoEmailService,
        ILogger<CustoFixoLembreteService> logger)
    {
        _custoFixoRepository = custoFixoRepository;
        _usuarioRepository = usuarioRepository;
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
        var usuarioIdsComNotificacao = await ObterUsuariosParaNotificacaoAsync(dataReferencia);
        if (!usuarioIdsComNotificacao.Any()) return;

        var custosCandidatos = await ObterCustosParaUsuariosAsync(usuarioIdsComNotificacao, dataReferencia);
        if (!custosCandidatos.Any()) return;

        var custosAgrupadosPorUsuario = custosCandidatos.GroupBy(x => x.UsuarioId);
        var usuariosDict = await ObterDicionarioUsuariosAsync(usuarioIdsComNotificacao);

        await ProcessarEnvioDeLembretesAsync(custosAgrupadosPorUsuario, usuariosDict, dataReferencia, tipo);
    }

    private async Task<List<string>> ObterUsuariosParaNotificacaoAsync(DateTime dataReferencia)
    {
        int diaVencimentoBuscado = dataReferencia.Day;
        bool ehUltimoDiaDoMes = diaVencimentoBuscado == DateTime.DaysInMonth(dataReferencia.Year, dataReferencia.Month);

        var usuarioIdsCandidatos = new List<string>();

        if (ehUltimoDiaDoMes)
        {
            for (int dia = diaVencimentoBuscado; dia <= 31; dia++)
            {
                var ids = await _custoFixoRepository.GetUsuarioIdsPorDiaVencimento(dia);
                usuarioIdsCandidatos.AddRange(ids);
            }
        }
        else
        {
            var ids = await _custoFixoRepository.GetUsuarioIdsPorDiaVencimento(diaVencimentoBuscado);
            usuarioIdsCandidatos.AddRange(ids);
        }

        usuarioIdsCandidatos = usuarioIdsCandidatos.Distinct().ToList();

        if (!usuarioIdsCandidatos.Any())
        {
            _logger.LogInformation("Nenhum custo fixo ativo encontrado com vencimento no dia {Dia}.", diaVencimentoBuscado);
            return new List<string>();
        }

        var usuarioIdsComNotificacao = await _usuarioRepository.FiltrarUsuariosComNotificacaoAtiva(usuarioIdsCandidatos);
        
        if (!usuarioIdsComNotificacao.Any())
        {
            _logger.LogInformation("Todos os donos de custos fixos do dia {Dia} desativaram as notificações globais.", diaVencimentoBuscado);
        }

        return usuarioIdsComNotificacao;
    }

    private async Task<List<Domain.Entity.CustoFixo>> ObterCustosParaUsuariosAsync(List<string> usuarioIds, DateTime dataReferencia)
    {
        int diaVencimentoBuscado = dataReferencia.Day;
        bool ehUltimoDiaDoMes = diaVencimentoBuscado == DateTime.DaysInMonth(dataReferencia.Year, dataReferencia.Month);
        var custosCandidatos = new List<Domain.Entity.CustoFixo>();

        if (ehUltimoDiaDoMes)
        {
            for (int dia = diaVencimentoBuscado; dia <= 31; dia++)
            {
                var custosDia = await _custoFixoRepository.GetCustosFixosPorUsuariosEDiaVencimento(usuarioIds, dia);
                custosCandidatos.AddRange(custosDia);
            }
        }
        else
        {
            var custosDia = await _custoFixoRepository.GetCustosFixosPorUsuariosEDiaVencimento(usuarioIds, diaVencimentoBuscado);
            custosCandidatos.AddRange(custosDia);
        }

        return custosCandidatos;
    }

    private async Task<Dictionary<string, Usuario>> ObterDicionarioUsuariosAsync(List<string> usuarioIds)
    {
        var usuarios = await _usuarioRepository.GetByIds(usuarioIds);
        return usuarios?.ToDictionary(x => x.Id) ?? new Dictionary<string, Usuario>();
    }

    private async Task ProcessarEnvioDeLembretesAsync(
        IEnumerable<IGrouping<string, Domain.Entity.CustoFixo>> custosAgrupados, 
        Dictionary<string, Usuario> usuariosDict,
        DateTime dataReferencia, 
        TipoLembrete tipo)
    {
        foreach (var grupo in custosAgrupados)
        {
            var usuarioId = grupo.Key;
            var custosDoUsuario = grupo.ToList();

            if (!usuariosDict.TryGetValue(usuarioId, out var usuario) || usuario == null)
            {
                _logger.LogWarning("Usuário {UsuarioId} dono de custos fixos não foi encontrado no banco de dados.", usuarioId);
                continue;
            }

            bool jaEnviado = await _historicoRepository.ExisteRegistroAsync(usuarioId, dataReferencia.Date, tipo);
            if (jaEnviado)
            {
                _logger.LogInformation("Lembrete do tipo {Tipo} para a data de vencimento {DataVencimento} já foi enviado para o usuário {Nome} ({UsuarioId}). Pulando.", tipo, dataReferencia.ToString("yyyy-MM-dd"), usuario.Nome, usuarioId);
                continue;
            }

            await EnviarERegistrarLembreteAsync(usuario, custosDoUsuario, dataReferencia, tipo);
        }
    }

    private async Task EnviarERegistrarLembreteAsync(Usuario usuario, List<Domain.Entity.CustoFixo> custos, DateTime dataReferencia, TipoLembrete tipo)
    {
        int diasRestantes = tipo == TipoLembrete.DiaDoVencimento ? 0 : 3;
        var itensEmail = custos.Select(c => new CustoFixoLembreteItem(c.Nome, diasRestantes)).ToList();

        _logger.LogInformation("Enviando lembrete de custo fixo real ({Tipo}) para {Email} ({Nome}). Vencimento: {DataVencimento}.",
            tipo, usuario.Email, usuario.Nome, dataReferencia.ToString("yyyy-MM-dd"));

        var resultadoEnvio = await _custoFixoEmailService.EnviarLembreteAsync(usuario.Email, usuario.Nome, itensEmail, tipo);

        if (resultadoEnvio.IsSucess)
        {
            _logger.LogInformation("Lembrete do tipo {Tipo} enviado com sucesso para {Email}.", tipo, usuario.Email);

            var historico = new CustoFixoLembreteHistorico(usuario.Id, dataReferencia.Date, tipo);
            try
            {
                await _historicoRepository.RegistrarEnvioAsync(historico);
                _logger.LogInformation("Registrada idempotência de lembrete do tipo {Tipo} na data {DataVencimento} para o usuário {UsuarioId}.", tipo, dataReferencia.ToString("yyyy-MM-dd"), usuario.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao registrar idempotência para o usuário {UsuarioId} na data {DataVencimento}. Possível duplicidade de thread prevenida.", usuario.Id, dataReferencia.ToString("yyyy-MM-dd"));
            }
        }
        else
        {
            _logger.LogError("Falha ao enviar e-mail de lembrete do tipo {Tipo} para {Email}. Erro: {ErroMessage}. Idempotência NÃO registrada para permitir retry na próxima execução.",
                tipo, usuario.Email, resultadoEnvio.Error?.Message);
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
