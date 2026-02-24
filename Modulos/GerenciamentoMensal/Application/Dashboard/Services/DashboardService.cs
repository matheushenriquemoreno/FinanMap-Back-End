using Application.Dashboard.Interfaces;
using Domain.Dashboard.Models;
using Domain.Login.Interfaces;
using Infra.Mongo.Repositorys;

namespace Application.Dashboard.Services;

public class DashboardService : IDashboardService
{
    private readonly IDashboardRepository _repository;
    private readonly IUsuarioLogado _usuarioLogado;

    public DashboardService(IDashboardRepository repository, IUsuarioLogado usuarioLogado)
    {
        _repository = repository;
        _usuarioLogado = usuarioLogado;
    }

    public async Task<Result<ResumoFinanceiroModel>> ObterResumoFinanceiro(string dataInicial, string dataFinal)
    {
        var validacao = ParseEValidarPeriodo(dataInicial, dataFinal);
        if (!validacao.IsSucess)
            return Result.Failure<ResumoFinanceiroModel>(validacao.Error);

        var p = validacao.Value;
        var usuarioId = _usuarioLogado.IdContextoDados;
        var resultado = await _repository.ObterResumoFinanceiro(usuarioId, p.MesInicial, p.AnoInicial, p.MesFinal, p.AnoFinal);

        return Result.Success(resultado);
    }

    public async Task<Result<List<EvolucaoPeriodoModel>>> ObterEvolucaoPeriodo(string dataInicial, string dataFinal)
    {
        var validacao = ParseEValidarPeriodo(dataInicial, dataFinal);
        if (!validacao.IsSucess)
            return Result.Failure<List<EvolucaoPeriodoModel>>(validacao.Error);

        var p = validacao.Value;
        var usuarioId = _usuarioLogado.IdContextoDados;
        var resultado = await _repository.ObterEvolucaoPeriodo(usuarioId, p.MesInicial, p.AnoInicial, p.MesFinal, p.AnoFinal);

        return Result.Success(resultado);
    }

    public async Task<Result<List<CategoriaDashboardModel>>> ObterDistribuicaoCategorias(string dataInicial, string dataFinal, string? tipo)
    {
        var validacao = ParseEValidarPeriodo(dataInicial, dataFinal);
        if (!validacao.IsSucess)
            return Result.Failure<List<CategoriaDashboardModel>>(validacao.Error);

        if (!string.IsNullOrEmpty(tipo) && tipo != "Rendimento" && tipo != "Despesa" && tipo != "Investimento")
            return Result.Failure<List<CategoriaDashboardModel>>(Error.Validation("O tipo deve ser 'Rendimento', 'Despesa' ou 'Investimento'"));

        var p = validacao.Value;
        var usuarioId = _usuarioLogado.IdContextoDados;
        var resultado = await _repository.ObterDistribuicaoCategorias(usuarioId, p.MesInicial, p.AnoInicial, p.MesFinal, p.AnoFinal, tipo);

        return Result.Success(resultado);
    }

    private Result<(int MesInicial, int AnoInicial, int MesFinal, int AnoFinal)> ParseEValidarPeriodo(string dataInicial, string dataFinal)
    {
        if (string.IsNullOrWhiteSpace(dataInicial) || dataInicial.Length != 7 || !dataInicial.Contains('-'))
            return Result.Failure<(int, int, int, int)>(Error.Validation("A data inicial deve estar no formato YYYY-MM"));

        if (string.IsNullOrWhiteSpace(dataFinal) || dataFinal.Length != 7 || !dataFinal.Contains('-'))
            return Result.Failure<(int, int, int, int)>(Error.Validation("A data final deve estar no formato YYYY-MM"));

        var partsIni = dataInicial.Split('-');
        var partsFim = dataFinal.Split('-');

        if (!int.TryParse(partsIni[0], out var anoInicial) || !int.TryParse(partsIni[1], out var mesInicial))
            return Result.Failure<(int, int, int, int)>(Error.Validation("A data inicial é inválida"));

        if (!int.TryParse(partsFim[0], out var anoFinal) || !int.TryParse(partsFim[1], out var mesFinal))
            return Result.Failure<(int, int, int, int)>(Error.Validation("A data final é inválida"));

        if (mesInicial < 1 || mesInicial > 12 || mesFinal < 1 || mesFinal > 12)
            return Result.Failure<(int, int, int, int)>(Error.Validation("O mês deve estar entre 1 e 12"));

        if (anoInicial < 2000 || anoInicial > 2100 || anoFinal < 2000 || anoFinal > 2100)
            return Result.Failure<(int, int, int, int)>(Error.Validation("O ano deve estar entre 2000 e 2100"));

        var periodoInicialNumber = anoInicial * 100 + mesInicial;
        var periodoFinalNumber = anoFinal * 100 + mesFinal;

        if (periodoInicialNumber > periodoFinalNumber)
            return Result.Failure<(int, int, int, int)>(Error.Validation("O período inicial não pode ser maior que o período final"));

        return Result.Success((mesInicial, anoInicial, mesFinal, anoFinal));
    }
}
