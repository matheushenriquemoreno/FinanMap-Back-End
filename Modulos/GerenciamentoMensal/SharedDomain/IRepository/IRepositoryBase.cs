using System.Linq.Expressions;
using SharedDomain.Entity;

namespace Domain;

public interface IRepositoryBase<T> where T : IEntityBase
{
    Task<T> Add(T entity);
    Task<List<T>> Add(List<T> entity);
    Task<T> Update(T entity);
    Task Delete(T entity);
    Task<T> GetById(string id);
    Task<List<T>> GetByIds(List<string> ids);
    Task<IEnumerable<T>> GetWhere(Expression<Func<T, bool>> filtro);
}

