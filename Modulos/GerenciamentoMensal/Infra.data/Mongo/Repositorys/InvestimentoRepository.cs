using Domain.Entity;
using Domain.Repository;
using Infra.Data.Mongo.RepositoryBase;
using MongoDB.Driver;

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

    public async Task<IEnumerable<Investimento>> ObterPeloMes(int mes, int ano, string usuarioId)
    {
        var investimentos = await _entityCollection.Find(x => x.Ano == ano && x.Mes == mes && x.UsuarioId == usuarioId)
            .ToListAsync();

        foreach (Investimento investimento in investimentos)
        {
            await IncluirDependencias(investimento);
        }

        return investimentos;
    }
}
