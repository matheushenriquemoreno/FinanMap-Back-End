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
    /// quando null, retorna todas. O filtro de ativas é tolerante a documentos
    /// antigos sem o campo "Arquivada" (criados antes da feature).
    /// </summary>
    public async Task<List<Domain.Entity.MetaFinanceira>> ObterPorUsuario(string usuarioId, bool? arquivadas = null)
    {
        return await _entityCollection
            .Find(ConstruirFiltroObterPorUsuario(usuarioId, arquivadas))
            .ToListAsync();
    }

    /// <summary>
    /// Constrói o filtro de listagem por usuário. Internal para permitir o teste
    /// da semântica do filtro (via renderização) sem depender de banco de dados.
    /// </summary>
    internal static FilterDefinition<Domain.Entity.MetaFinanceira> ConstruirFiltroObterPorUsuario(
        string usuarioId, bool? arquivadas)
    {
        var filtro = Builders<Domain.Entity.MetaFinanceira>.Filter.Eq(m => m.UsuarioId, usuarioId);

        if (!arquivadas.HasValue)
            return filtro;

        if (arquivadas.Value)
        {
            filtro &= Builders<Domain.Entity.MetaFinanceira>.Filter.Eq(m => m.Arquivada, true);
        }
        else
        {
            // Metas criadas antes da feature de arquivamento não possuem o campo
            // "Arquivada" no documento. O Mongo com Eq(false) NÃO casa documentos
            // sem o campo, o que faria metas antigas sumirem do filtro "Ativas".
            // Ne(true) casa tanto false quanto campo ausente, mantendo-as visíveis.
            filtro &= Builders<Domain.Entity.MetaFinanceira>.Filter.Ne(m => m.Arquivada, true);
        }

        return filtro;
    }
}
