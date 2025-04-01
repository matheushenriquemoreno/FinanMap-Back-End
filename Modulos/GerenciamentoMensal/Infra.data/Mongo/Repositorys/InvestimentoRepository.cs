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
}
