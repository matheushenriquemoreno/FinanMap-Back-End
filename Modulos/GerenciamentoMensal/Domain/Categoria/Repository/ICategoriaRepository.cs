using System.Linq.Expressions;
using Domain.Entity;
using Domain.Enum;

namespace Domain.Repository;

public interface ICategoriaRepository : IRepositoryBase<Categoria>
{
    IQueryable<Categoria> GetCategorias();
    bool CategoriaJaExiste(string nome, string idUsuario, TipoCategoria tipo);
    Task<List<Categoria>> GetCategorias(TipoCategoria tipoCategoria, string nome, string idUsuario);
    Task<bool> CategoriaPossuiVinculo(Categoria Categoria);
}
