using Domain.Entity;
using Domain.Repository;
using Infra.Data.Mongo.RepositoryBase;
using MongoDB.Bson;
using MongoDB.Driver;

#nullable enable annotations

namespace Infra.Data.Mongo.Repositorys;

public class DespesaRepository : RepositoryTransacaoBase<Despesa>, IDespesaRepository
{
    public DespesaRepository(IMongoClient mongoClient, ICategoriaRepository categoriaRepository) : base(mongoClient, categoriaRepository)
    {
    }

    public override string GetCollectionName()
    {
        return nameof(Despesa);
    }

    public Task<IEnumerable<Despesa>> GetPeloMes(int mes, int ano, string usuarioId, string descricao)
    {
        var filtros = new List<FilterDefinition<Despesa>>();
        var filterDefinition = Builders<Despesa>.Filter;

        if (!string.IsNullOrEmpty(descricao))
        {

            var filtro = filterDefinition.Regex(x => x.Descricao, new BsonRegularExpression(descricao, "i"));
            filtros.Add(filtro);
        }

        filtros.Add(filterDefinition.Eq(x => x.IdDespesaAgrupadora, null));

        return ObterPeloMesFilter(mes, ano, usuarioId, filtros);
    }

    public async Task<decimal> GetValorTotalDespesasDaAgrupadora(string idDespesaAgrupadora)
    {
        var despesas = await _entityCollection.Find(x => x.IdDespesaAgrupadora == idDespesaAgrupadora).ToListAsync();
        return despesas.Sum(x => x.Valor);
    }

    public async Task<IEnumerable<Despesa>> GetDespesasDaAgrupadora(string idDespesaAgrupadora)
    {
        var despesas = await _entityCollection.Find(x => x.IdDespesaAgrupadora == idDespesaAgrupadora).ToListAsync();

        foreach (var despesa in despesas)
        {
            await IncluirDependencias(despesa);
        }

        return despesas;
    }

    protected override async Task IncluirDependencias(Despesa despesa)
    {
        await base.IncluirDependencias(despesa);

        if (despesa.EstaAgrupada())
        {
            despesa.Agrupadora = await GetById(despesa.IdDespesaAgrupadora);
        }
    }

    public async Task<IEnumerable<Despesa>> GetDespesasDoLoteAsync(string despesaOrigemId)
    {
        var filter = Builders<Despesa>.Filter.Eq(x => x.DespesaOrigemId, despesaOrigemId);
        return await _entityCollection.Find(filter).ToListAsync();
    }

    public async Task InsertManyAsync(IEnumerable<Despesa> despesas)
    {
        if (despesas.Any())
            await _entityCollection.InsertManyAsync(despesas);
    }

    public async Task UpdateManyAsync(IEnumerable<Despesa> despesas)
    {
        var models = new List<WriteModel<Despesa>>();
        foreach (var despesa in despesas)
        {
            var filter = Builders<Despesa>.Filter.Eq(x => x.Id, despesa.Id);
            models.Add(new ReplaceOneModel<Despesa>(filter, despesa));
        }

        if (models.Any())
        {
            await _entityCollection.BulkWriteAsync(models);
        }
    }

    public async Task DeleteManyAsync(IEnumerable<Despesa> despesas)
    {
        var ids = despesas.Select(x => x.Id).ToList();

        if (ids.Any())
        {
            var filter = Builders<Despesa>.Filter.In(x => x.Id, ids);
            await _entityCollection.DeleteManyAsync(filter);
        }
    }

    public Task<Despesa?> TryUpdateMcpAsync(
        string id,
        string userId,
        McpExpenseSnapshot expected,
        McpExpenseSnapshot proposed,
        string operationId,
        string resultHash,
        CancellationToken cancellationToken = default)
    {
        var filter = SnapshotFilter(id, userId, expected);
        var update = Builders<Despesa>.Update
            .Set(item => item.Descricao, proposed.Description)
            .Set(item => item.Valor, proposed.Amount)
            .Set(item => item.CategoriaId, proposed.CategoryId)
            .Set(item => item.IdDespesaAgrupadora, proposed.GroupingExpenseId)
            .Set(item => item.DespesaOrigemId, proposed.ExpenseOriginId)
            .Set(item => item.IsParcelado, proposed.IsInstallment)
            .Set(item => item.IsRecorrente, proposed.IsRecurring)
            .Set(item => item.ParcelaAtual, proposed.InstallmentNumber)
            .Set(item => item.TotalParcelas, proposed.InstallmentCount)
            .Set(item => item.LastMcpOperationId, operationId)
            .Set(item => item.LastMcpResultHash, resultHash);
        return _entityCollection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<Despesa>
            {
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);
    }

    public async Task<bool> TryDeleteMcpAsync(
        string id,
        string userId,
        McpExpenseSnapshot expected,
        CancellationToken cancellationToken = default)
    {
        var result = await _entityCollection.DeleteOneAsync(
            SnapshotFilter(id, userId, expected),
            cancellationToken);
        return result.DeletedCount == 1;
    }

    public async Task<Despesa?> TrySynchronizeGroupingMcpAsync(
        string groupingExpenseId,
        string userId,
        decimal expectedParentAmount,
        decimal baseAmount,
        string operationId,
        string resultHash,
        CancellationToken cancellationToken = default)
    {
        var children = await _entityCollection.Find(item =>
                item.IdDespesaAgrupadora == groupingExpenseId &&
                item.UsuarioId == userId)
            .ToListAsync(cancellationToken);
        var update = Builders<Despesa>.Update
            .Set(item => item.Valor, baseAmount + children.Sum(item => item.Valor))
            .Set(item => item.QuantidadeRegistros, children.Count)
            .Set(item => item.DespesaAgrupadora, children.Count > 0)
            .Set(item => item.LastMcpOperationId, operationId)
            .Set(item => item.LastMcpResultHash, resultHash);
        return await _entityCollection.FindOneAndUpdateAsync(
            item =>
                item.Id == groupingExpenseId &&
                item.UsuarioId == userId &&
                item.Valor == expectedParentAmount,
            update,
            new FindOneAndUpdateOptions<Despesa>
            {
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);
    }

    private static FilterDefinition<Despesa> SnapshotFilter(
        string id,
        string userId,
        McpExpenseSnapshot expected) =>
        Builders<Despesa>.Filter.Where(item =>
            item.Id == id &&
            item.UsuarioId == userId &&
            item.Ano == expected.Year &&
            item.Mes == expected.Month &&
            item.Descricao == expected.Description &&
            item.Valor == expected.Amount &&
            item.CategoriaId == expected.CategoryId &&
            item.IdDespesaAgrupadora == expected.GroupingExpenseId &&
            item.DespesaOrigemId == expected.ExpenseOriginId &&
            item.IsParcelado == expected.IsInstallment &&
            item.IsRecorrente == expected.IsRecurring &&
            item.ParcelaAtual == expected.InstallmentNumber &&
            item.TotalParcelas == expected.InstallmentCount);
}

#nullable restore annotations
