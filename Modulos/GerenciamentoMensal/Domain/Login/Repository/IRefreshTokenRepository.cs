using Domain.Login.Entity;

namespace Domain.Login.Repository;

public interface IRefreshTokenRepository
{
    Task<RefreshToken> Add(RefreshToken entity);
    Task<RefreshToken> GetByToken(string token);
    Task Delete(RefreshToken entity);
    Task DeleteByUsuarioId(string usuarioId);
}
