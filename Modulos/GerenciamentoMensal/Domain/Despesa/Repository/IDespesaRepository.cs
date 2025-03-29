using Domain.Entity;

namespace Domain.Repository;

public interface IDespesaRepository : IRepositoryBase<Despesa>
{
    public Task<IEnumerable<Despesa>> ObterPeloMes(int mes, int ano, string usuarioId);
}
