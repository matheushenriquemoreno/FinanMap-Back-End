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

    public async Task<bool> CategoriaPossuiVinculo(Categoria Categoria)
    {
        switch (Categoria.Tipo)
        {
            case TipoCategoria.Investimento:
                return await CategoriaVinculadaATransacao<Investimento>(Categoria.Id);
            case TipoCategoria.Despesa:
                return await CategoriaVinculadaATransacao<Despesa>(Categoria.Id);
            case TipoCategoria.Rendimento:
                return await CategoriaVinculadaATransacao<Rendimento>(Categoria.Id);
        }

        return false;
    }

    private async Task<bool> CategoriaVinculadaATransacao<T>(string idCategoria) where T : Transacao
    {
        var filtro = Builders<T>.Filter.Eq(x => x.CategoriaId, idCategoria);

        var result = await _mongoClient.GetCollection<T>(_database).CountDocumentsAsync(filtro);

        return result > 0;
    }

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

        return await _entityCollection.Find(resultFilter).ToListAsync();
    }
}
