using Domain.Entity;

namespace Domain.Repository;

public interface IDespesaRepository : IRepositoryTransacaoBase<Despesa>
{
    Task<decimal> ObterValorTotalDespesasDaAgrupadora(string idDespesaAgrupadora);
    Task<IEnumerable<Despesa>> ObterPeloMes(int mes, int ano, string usuarioId, string descricao);
    Task<IEnumerable<Despesa>> ObterDespesasDaAgrupadora(string idDespesaAgrupadora);
}
