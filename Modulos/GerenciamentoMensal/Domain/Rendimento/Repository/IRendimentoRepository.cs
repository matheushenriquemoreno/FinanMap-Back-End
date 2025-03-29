using Domain.Entity;

namespace Domain.Repository;

public interface IRendimentoRepository : IRepositoryBase<Rendimento>
{
    public Task<IEnumerable<Rendimento>> ObterPeloMes(int mes, int ano, string usuarioId);
}
