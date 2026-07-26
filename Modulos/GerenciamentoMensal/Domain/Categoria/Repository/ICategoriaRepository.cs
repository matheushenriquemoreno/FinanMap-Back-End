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
    Task<Categoria?> TryUpdateMcpAsync(
        string id,
        string userId,
        string expectedName,
        TipoCategoria expectedType,
        string proposedName,
        TipoCategoria proposedType,
        string operationId,
        string resultHash,
        CancellationToken cancellationToken = default) =>
        Task.FromException<Categoria?>(
            new NotSupportedException("CAS MCP de categoria não implementado."));
    Task<bool> TryDeleteMcpAsync(
        string id,
        string userId,
        string expectedName,
        TipoCategoria expectedType,
        CancellationToken cancellationToken = default) =>
        Task.FromException<bool>(
            new NotSupportedException("Exclusão MCP de categoria não implementada."));
}
