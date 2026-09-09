using Domain.Entity;

namespace Domain.Repository;

public interface ICompraPlanejadaRepository : IRepositoryBase<CompraPlanejada>
{
    Task<CompraPlanejada> GetById(string id, string usuarioId);
    Task<List<CompraPlanejada>> GetPendentes(string usuarioId);
    Task<List<CompraPlanejada>> GetComprados(string usuarioId);
}
