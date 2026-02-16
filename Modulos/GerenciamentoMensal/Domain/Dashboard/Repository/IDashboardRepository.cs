using Domain.Dashboard.Models;

namespace Infra.Mongo.Repositorys;

public interface IDashboardRepository
{
    Task<ResumoFinanceiroModel> ObterResumoFinanceiro(string usuarioId, int mesInicial, int mesFinal, int ano);
    Task<List<EvolucaoPeriodoModel>> ObterEvolucaoPeriodo(string usuarioId, int mesInicial, int mesFinal, int ano);
    Task<List<CategoriaDashboardModel>> ObterDistribuicaoCategorias(string usuarioId, int mesInicial, int mesFinal, int ano, string? tipo);
}
