using Domain;
using Domain.Entity;

namespace Domain.MetaFinanceira.Repository;

public interface IMetaFinanceiraRepository : IRepositoryBase<Entity.MetaFinanceira>
{
    Task<List<Entity.MetaFinanceira>> ObterPorUsuario(string usuarioId);
}
