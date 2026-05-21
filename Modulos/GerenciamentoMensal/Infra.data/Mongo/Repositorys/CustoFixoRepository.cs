using System.Text.RegularExpressions;
using Domain.Entity;
using Domain.Repository;
using Infra.Data.Mongo.RepositoryBase;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Repositorys;

public class CustoFixoRepository : RepositoryMongoBase<CustoFixo>, ICustoFixoRepository
{
    public CustoFixoRepository(IMongoClient mongoClient) : base(mongoClient)
    {
    }

    public override string GetCollectionName()
        => "CustosFixos";

    public async Task<List<CustoFixo>> GetByUsuarioId(string usuarioId)
    {
        var filtro = Builders<CustoFixo>.Filter.Eq(x => x.UsuarioId, usuarioId);

        return await _entityCollection
            .Find(filtro)
            .SortBy(x => x.DiaVencimento)
            .ThenBy(x => x.Nome)
            .ToListAsync();
    }

    public async Task<bool> ExisteAtivoDuplicado(string usuarioId, string nome, int diaVencimento, string ignorarId = null)
    {
        var builder = Builders<CustoFixo>.Filter;

        var filtro = builder.And(
            builder.Eq(x => x.UsuarioId, usuarioId),
            builder.Regex(x => x.Nome, new BsonRegularExpression($"^{Regex.Escape(nome)}$", "i")),
            builder.Eq(x => x.DiaVencimento, diaVencimento),
            builder.Eq(x => x.Ativo, true));

        if (!string.IsNullOrWhiteSpace(ignorarId))
            filtro = builder.And(filtro, builder.Ne(x => x.Id, ignorarId));

        return await _entityCollection.Find(filtro).AnyAsync();
    }
}
