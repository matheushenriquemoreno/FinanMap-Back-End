using Domain.Entity;

namespace Domain.Repository;

public interface IDespesaRepository : IRepositoryTransacaoBase<Despesa>
{
    Task<decimal> GetValorTotalDespesasDaAgrupadora(string idDespesaAgrupadora);
    Task<IEnumerable<Despesa>> GetPeloMes(int mes, int ano, string usuarioId, string descricao);
    Task<IEnumerable<Despesa>> GetDespesasDaAgrupadora(string idDespesaAgrupadora);
}
