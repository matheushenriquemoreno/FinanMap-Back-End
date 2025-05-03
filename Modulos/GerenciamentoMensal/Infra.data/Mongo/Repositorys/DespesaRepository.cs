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

    public Task<IEnumerable<Despesa>> ObterPeloMes(int mes, int ano, string usuarioId, string descricao)
    {
        var filtros = new List<FilterDefinition<Despesa>>();
        var filterDefinition = Builders<Despesa>.Filter;

        if (!string.IsNullOrEmpty(descricao)){

            var filtro = filterDefinition.Regex(x => x.Descricao, new BsonRegularExpression(descricao, "i"));
            filtros.Add(filtro);
        }

        filtros.Add(filterDefinition.Eq(x => x.IdDespesaAgrupadora, null));

        return ObterPeloMesFilter(mes, ano, usuarioId, filtros);
    }

    public async Task<decimal> ObterValorTotalDespesasDaAgrupadora(string idDespesaAgrupadora)
    {
        var despesas = await _entityCollection.Find(x => x.IdDespesaAgrupadora == idDespesaAgrupadora).ToListAsync();
        return despesas.Sum(x => x.Valor);
    }

    public async Task<IEnumerable<Despesa>> ObterDespesasDaAgrupadora(string idDespesaAgrupadora)
    {
        var despesas = await _entityCollection.Find(x => x.IdDespesaAgrupadora == idDespesaAgrupadora).ToListAsync();

        foreach (var despesa in despesas)
        {
            await IncluirDependencias(despesa);
        }

        return despesas;
    }
}
