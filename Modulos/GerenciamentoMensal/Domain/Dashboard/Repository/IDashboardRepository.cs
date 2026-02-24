using Domain.Dashboard.Models;

namespace Infra.Mongo.Repositorys;

public interface IDashboardRepository
{
    Task<ResumoFinanceiroModel> ObterResumoFinanceiro(string usuarioId, int mesInicial, int anoInicial, int mesFinal, int anoFinal);
    Task<List<EvolucaoPeriodoModel>> ObterEvolucaoPeriodo(string usuarioId, int mesInicial, int anoInicial, int mesFinal, int anoFinal);
    Task<List<CategoriaDashboardModel>> ObterDistribuicaoCategorias(string usuarioId, int mesInicial, int anoInicial, int mesFinal, int anoFinal, string? tipo);
}
