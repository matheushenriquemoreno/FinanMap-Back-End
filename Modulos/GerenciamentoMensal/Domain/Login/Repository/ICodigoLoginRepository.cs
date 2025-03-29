using Domain.Login.Entity;

namespace Domain.Login.Repository;

public interface ICodigoLoginRepository
{
    Task<CodigoLogin> Add(CodigoLogin entity);
    Task Delete(CodigoLogin entity);
    Task<CodigoLogin> GetByEmail(string Email);
    Task<CodigoLogin> GetByCodigo(string codigo);
    Task DeleteExpirados(string email);
}
