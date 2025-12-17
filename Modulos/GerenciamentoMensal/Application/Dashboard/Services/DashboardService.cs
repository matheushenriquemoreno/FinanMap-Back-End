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

    public async Task<Result<ResumoFinanceiroModel>> ObterResumoFinanceiro(int mesInicial, int mesFinal, int ano)
    {
        var validacao = ValidarParametros(mesInicial, mesFinal, ano);
        if (!validacao.IsSucess)
            return Result.Failure<ResumoFinanceiroModel>(validacao.Error);

        var usuarioId = _usuarioLogado.Id;
        var resultado = await _repository.ObterResumoFinanceiro(usuarioId, mesInicial, mesFinal, ano);

        return Result.Success(resultado);
    }

    public async Task<Result<List<EvolucaoPeriodoModel>>> ObterEvolucaoPeriodo(int mesInicial, int mesFinal, int ano)
    {
        var validacao = ValidarParametros(mesInicial, mesFinal, ano);
        if (!validacao.IsSucess)
            return Result.Failure<List<EvolucaoPeriodoModel>>(validacao.Error);

        var usuarioId = _usuarioLogado.Id;
        var resultado = await _repository.ObterEvolucaoPeriodo(usuarioId, mesInicial, mesFinal, ano);

        return Result.Success(resultado);
    }

    public async Task<Result<List<CategoriaDashboardModel>>> ObterDistribuicaoCategorias(int mesInicial, int mesFinal, int ano, string? tipo)
    {
        var validacao = ValidarParametros(mesInicial, mesFinal, ano);
        if (!validacao.IsSucess)
            return Result.Failure<List<CategoriaDashboardModel>>(validacao.Error);

        if (!string.IsNullOrEmpty(tipo) && tipo != "Rendimento" && tipo != "Despesa" && tipo != "Investimento")
            return Result.Failure<List<CategoriaDashboardModel>>(Error.Validation("O tipo deve ser 'Rendimento', 'Despesa' ou 'Investimento'"));

        var usuarioId = _usuarioLogado.Id;
        var resultado = await _repository.ObterDistribuicaoCategorias(usuarioId, mesInicial, mesFinal, ano, tipo);

        return Result.Success(resultado);
    }

    private Result ValidarParametros(int mesInicial, int mesFinal, int ano)
    {
        if (mesInicial < 1 || mesInicial > 12)
            return Result.Failure(Error.Validation("O mês inicial deve estar entre 1 e 12"));

        if (mesFinal < 1 || mesFinal > 12)
            return Result.Failure(Error.Validation("O mês final deve estar entre 1 e 12"));

        if (ano < 2000 || ano > 2100)
            return Result.Failure(Error.Validation("O ano deve estar entre 2000 e 2100"));

        return Result.Success();
    }
}
