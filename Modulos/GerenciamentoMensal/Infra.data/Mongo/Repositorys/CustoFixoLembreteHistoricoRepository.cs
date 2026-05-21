using Domain.Entity;
using Domain.Enum;
using Domain.Repository;
using Infra.Data.Mongo.RepositoryBase;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Repositorys;

public class CustoFixoLembreteHistoricoRepository : RepositoryMongoBase<CustoFixoLembreteHistorico>, ICustoFixoLembreteHistoricoRepository
{
    public CustoFixoLembreteHistoricoRepository(IMongoClient mongoClient) : base(mongoClient)
    {
    }

    public override string GetCollectionName() => "CustosFixosLembretesHistorico";

    public async Task<bool> ExisteRegistroAsync(string usuarioId, DateTime dataReferencia, TipoLembrete tipo)
    {
        var builder = Builders<CustoFixoLembreteHistorico>.Filter;
        var filtro = builder.And(
            builder.Eq(x => x.UsuarioId, usuarioId),
            builder.Eq(x => x.DataReferencia, dataReferencia.Date),
            builder.Eq(x => x.TipoLembrete, tipo)
        );

        return await _entityCollection.Find(filtro).AnyAsync();
    }

    public async Task RegistrarEnvioAsync(CustoFixoLembreteHistorico historico)
    {
        historico.DataReferencia = historico.DataReferencia.Date;
        await Add(historico);
    }
}
