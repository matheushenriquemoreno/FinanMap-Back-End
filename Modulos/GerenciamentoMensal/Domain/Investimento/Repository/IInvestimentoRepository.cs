using Domain.Entity;

namespace Domain.Repository;

public interface IInvestimentoRepository : IRepositoryBase<Investimento>
{
    public Task<IEnumerable<Investimento>> ObterPeloMes(int mes, int ano, string usuarioId);
}
