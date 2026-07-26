using Domain.Entity;
using Domain.Repository;
using Infra.Data.Mongo.RepositoryBase;
using MongoDB.Driver;

#nullable enable annotations

namespace Infra.Data.Mongo.Repositorys;

public class InvestimentoRepository : RepositoryTransacaoBase<Investimento>, IInvestimentoRepository
{
    public InvestimentoRepository(IMongoClient mongoClient, ICategoriaRepository categoriaRepository) : base(mongoClient, categoriaRepository)
    {
    }

    public override string GetCollectionName()
    {
        return nameof(Investimento);
    }

    public Task<Investimento?> TryUpdateMcpAsync(
        string id,
        string userId,
        McpInvestmentSnapshot expected,
        McpInvestmentSnapshot proposed,
        string operationId,
        string resultHash,
        CancellationToken cancellationToken = default)
    {
        var filter = SnapshotFilter(id, userId, expected);
        var update = Builders<Investimento>.Update
            .Set(item => item.Descricao, proposed.Description)
            .Set(item => item.Valor, proposed.Amount)
            .Set(item => item.CategoriaId, proposed.CategoryId)
            .Set(item => item.LastMcpOperationId, operationId)
            .Set(item => item.LastMcpResultHash, resultHash);
        return _entityCollection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<Investimento>
            {
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);
    }

    public async Task<bool> TryDeleteMcpAsync(
        string id,
        string userId,
        McpInvestmentSnapshot expected,
        CancellationToken cancellationToken = default)
    {
        var result = await _entityCollection.DeleteOneAsync(
            SnapshotFilter(id, userId, expected),
            cancellationToken);
        return result.DeletedCount == 1;
    }

    private static FilterDefinition<Investimento> SnapshotFilter(
        string id,
        string userId,
        McpInvestmentSnapshot expected) =>
        Builders<Investimento>.Filter.Where(item =>
            item.Id == id &&
            item.UsuarioId == userId &&
            item.Ano == expected.Year &&
            item.Mes == expected.Month &&
            item.Descricao == expected.Description &&
            item.Valor == expected.Amount &&
            item.CategoriaId == expected.CategoryId);
}

#nullable restore annotations
