using Domain.Dashboard.Models;

namespace Application.Dashboard.Interfaces;

public interface IDashboardService
{
    Task<Result<ResumoFinanceiroModel>> ObterResumoFinanceiro(int mesInicial, int mesFinal, int ano);
    Task<Result<List<EvolucaoPeriodoModel>>> ObterEvolucaoPeriodo(int mesInicial, int mesFinal, int ano);
    Task<Result<List<CategoriaDashboardModel>>> ObterDistribuicaoCategorias(int mesInicial, int mesFinal, int ano, string? tipo);
}
