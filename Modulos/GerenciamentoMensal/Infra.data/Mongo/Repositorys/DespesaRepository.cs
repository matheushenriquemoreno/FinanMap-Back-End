using Domain.Entity;
using Domain.Repository;
using Infra.Data.Mongo.RepositoryBase;
using MongoDB.Bson;
using MongoDB.Driver;

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
}
