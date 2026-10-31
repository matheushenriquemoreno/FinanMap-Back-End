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

    /// <summary>
    /// Obtém as metas do usuário. Quando <paramref name="arquivadas"/> for informado,
    /// filtra por estado de arquivamento (true = só arquivadas, false = só ativas);
    /// quando null, retorna todas.
    /// </summary>
    public async Task<List<Domain.Entity.MetaFinanceira>> ObterPorUsuario(string usuarioId, bool? arquivadas = null)
    {
        var filtro = Builders<Domain.Entity.MetaFinanceira>.Filter.Eq(m => m.UsuarioId, usuarioId);

        if (arquivadas.HasValue)
            filtro &= Builders<Domain.Entity.MetaFinanceira>.Filter.Eq(m => m.Arquivada, arquivadas.Value);

        return await _entityCollection
            .Find(filtro)
            .ToListAsync();
    }
}
