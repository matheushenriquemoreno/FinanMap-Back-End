using Domain.Dashboard.Models;

namespace Application.Dashboard.Interfaces;

public interface IDashboardService
{
    Task<Result<ResumoFinanceiroModel>> ObterResumoFinanceiro(string dataInicial, string dataFinal);
    Task<Result<List<EvolucaoPeriodoModel>>> ObterEvolucaoPeriodo(string dataInicial, string dataFinal);
    Task<Result<List<CategoriaDashboardModel>>> ObterDistribuicaoCategorias(string dataInicial, string dataFinal, string? tipo);
}
