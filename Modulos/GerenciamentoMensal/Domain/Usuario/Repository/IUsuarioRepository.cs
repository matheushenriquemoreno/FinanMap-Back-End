using Domain.Entity;

namespace Domain.Repository;

public interface IUsuarioRepository : IRepositoryBase<Usuario>
{
    Task<Usuario> GetByEmail(string email);
}
