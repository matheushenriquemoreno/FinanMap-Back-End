using System.Text.RegularExpressions;
using Domain.Entity;
using Domain.Repository;
using Infra.Data.Mongo.RepositoryBase;
using MongoDB.Bson;
using MongoDB.Driver;

#nullable enable annotations

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

    public async Task<List<CustoFixo>> GetCustosFixosAtivosPorDiaVencimento(int diaVencimento)
    {
        var builder = Builders<CustoFixo>.Filter;
        var filtro = builder.And(
            builder.Eq(x => x.DiaVencimento, diaVencimento),
            builder.Eq(x => x.Ativo, true)
        );

        return await _entityCollection.Find(filtro).ToListAsync();
    }

    public async Task<List<string>> GetUsuarioIdsPorDiaVencimento(int diaVencimento)
    {
        var builder = Builders<CustoFixo>.Filter;
        var filtro = builder.And(
            builder.Eq(x => x.DiaVencimento, diaVencimento),
            builder.Eq(x => x.Ativo, true)
        );

        return await _entityCollection.Distinct(x => x.UsuarioId, filtro).ToListAsync();
    }

    public async Task<List<CustoFixo>> GetCustosFixosPorUsuariosEDiaVencimento(List<string> usuarioIds, int diaVencimento)
    {
        var builder = Builders<CustoFixo>.Filter;
        var filtro = builder.And(
            builder.In(x => x.UsuarioId, usuarioIds),
            builder.Eq(x => x.DiaVencimento, diaVencimento),
            builder.Eq(x => x.Ativo, true)
        );

        return await _entityCollection.Find(filtro).ToListAsync();
    }

    public Task<CustoFixo?> TryUpdateMcpAsync(
        string id,
        string userId,
        McpFixedCostSnapshot expected,
        McpFixedCostSnapshot proposed,
        string operationId,
        string resultHash,
        CancellationToken cancellationToken = default)
    {
        var filter = SnapshotFilter(id, userId, expected);
        var update = Builders<CustoFixo>.Update
            .Set(item => item.Nome, proposed.Name)
            .Set(item => item.DiaVencimento, proposed.DueDay)
            .Set(item => item.CategoriaId, proposed.CategoryId)
            .Set(item => item.Ativo, proposed.Active)
            .Set(item => item.LastMcpOperationId, operationId)
            .Set(item => item.LastMcpResultHash, resultHash);
        return _entityCollection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<CustoFixo>
            {
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);
    }

    public async Task<bool> TryDeleteMcpAsync(
        string id,
        string userId,
        McpFixedCostSnapshot expected,
        CancellationToken cancellationToken = default)
    {
        var result = await _entityCollection.DeleteOneAsync(
            SnapshotFilter(id, userId, expected),
            cancellationToken);
        return result.DeletedCount == 1;
    }

    private static FilterDefinition<CustoFixo> SnapshotFilter(
        string id,
        string userId,
        McpFixedCostSnapshot expected) =>
        Builders<CustoFixo>.Filter.Where(item =>
            item.Id == id &&
            item.UsuarioId == userId &&
            item.Nome == expected.Name &&
            item.DiaVencimento == expected.DueDay &&
            item.CategoriaId == expected.CategoryId &&
            item.Ativo == expected.Active);
}

#nullable restore annotations
