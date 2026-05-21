using Domain.Entity;

namespace Domain.Repository;

public interface ICustoFixoRepository : IRepositoryBase<CustoFixo>
{
    Task<List<CustoFixo>> GetByUsuarioId(string usuarioId);
    Task<bool> ExisteAtivoDuplicado(string usuarioId, string nome, int diaVencimento, string ignorarId = null);
}
