using Domain.Entity;
using Domain.MetaFinanceira.Repository;
using Infra.Data.Mongo.RepositoryBase;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Repositorys;

public class MetaFinanceiraRepository : RepositoryMongoBase<Domain.Entity.MetaFinanceira>, IMetaFinanceiraRepository
{
    public MetaFinanceiraRepository(IMongoClient mongoClient) : base(mongoClient)
    {
    }

    public override string GetCollectionName()
    {
        return "MetaFinanceira";
    }

    public async Task<List<Domain.Entity.MetaFinanceira>> ObterPorUsuario(string usuarioId)
    {
        return await _entityCollection
            .Find(Builders<Domain.Entity.MetaFinanceira>.Filter.Eq(m => m.UsuarioId, usuarioId))
            .ToListAsync();
    }
}
