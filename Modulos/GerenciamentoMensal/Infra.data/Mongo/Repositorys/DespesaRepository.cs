using Domain.Entity;
using Domain.Repository;
using Infra.Data.Mongo.RepositoryBase;
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

    public async Task<IEnumerable<Despesa>> ObterPeloMes(int mes, int ano, string usuarioId)
    {
        var despesas = await _entityCollection.Find(x => x.Ano == ano && x.Mes == mes && x.UsuarioId == x.UsuarioId).ToListAsync();

        foreach (var despesa in despesas)
        {
            await IncluirDependencias(despesa);
        }

        return despesas;
    }
}
