using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Domain.Entity;
using Domain.Enum;
using Domain.Repository;
using Infra.Data.Mongo.RepositoryBase;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Repositorys;

public class CategoriaRepository : RepositoryMongoBase<Categoria>, ICategoriaRepository
{
    public CategoriaRepository(IMongoClient mongoClient) : base(mongoClient)
    {
    }
    private static FilterDefinition<Categoria> FiltrarNomeCategoriaIgnorandoCaseSensitive(string nome, FilterDefinitionBuilder<Categoria> builder)
    {
        return builder
            .Regex(x => x.Nome, new BsonRegularExpression($"^{Regex.Escape(nome)}$", "i"));
    }

    public IQueryable<Categoria> GetCategorias()
    {
        return _entityCollection.AsQueryable();
    }

    public Task<List<Categoria>> GetCategoriasOnde(Expression<Func<Categoria, bool>> filtro)
    {
        return _entityCollection.Find(filtro).ToListAsync();
    }

    public override string GetCollectionName()
        => nameof(Categoria);

    public bool CategoriaJaExiste(string nome, string idUsuario, TipoCategoria tipo)
    {
        var builder = Builders<Categoria>.Filter;

        FilterDefinition<Categoria> filtroNome = FiltrarNomeCategoriaIgnorandoCaseSensitive(nome, builder);
        FilterDefinition<Categoria> filtroCategoria = builder.Eq(x => x.Tipo, tipo);
        FilterDefinition<Categoria> filtroIdUsuario = builder.Eq(x => x.UsuarioId, idUsuario);

        return _entityCollection.Find(builder.And(filtroNome, filtroCategoria, filtroIdUsuario))
            .Any();
    }

    public async Task<List<Categoria>> GetCategorias(TipoCategoria tipoCategoria, string nome, string idUsuario)
    {
        var builder = Builders<Categoria>.Filter;

        FilterDefinition<Categoria> filtroCategoria = builder.Eq(x => x.Tipo, tipoCategoria);
        FilterDefinition<Categoria> filtroIdUsuario = builder.Eq(x => x.UsuarioId, idUsuario);

        var resultFilter = builder.And(filtroCategoria, filtroIdUsuario);

        if (!string.IsNullOrWhiteSpace(nome))
        {
            FilterDefinition<Categoria> filtroNome = builder.Where(x => x.Nome.ToLower().Contains(nome.ToLower()));
            resultFilter = builder.And(resultFilter, filtroNome);
        }

        var results = await _entityCollection.FindAsync(resultFilter);

        return results.ToList();
    }
}
