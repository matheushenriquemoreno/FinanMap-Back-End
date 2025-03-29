using System.Linq.Expressions;
using SharedDomain.Entity;

namespace Domain;

public interface IRepositoryBase<T> where T : IEntityBase
{
    Task<T> Add(T entity);
    Task<T> Update(T entity);
    Task Delete(T entity);
    Task<T> GetByID(string id);
    Task<IEnumerable<T>> GetWhere(Expression<Func<T, bool>> filtro);
}

