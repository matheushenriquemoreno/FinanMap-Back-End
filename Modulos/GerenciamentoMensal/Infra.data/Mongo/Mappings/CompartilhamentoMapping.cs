using Domain.Compartilhamento.Entity;
using Infra.Data.Mongo.Config.Interface;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Mappings;

public class CompartilhamentoMapping : IMongoMapping
{
    public void RegisterMap(IMongoClient mongoClient)
    {
        BsonClassMap.TryRegisterClassMap<Compartilhamento>(cm =>
        {
            cm.AutoMap();
            cm.MapMember(x => x.ProprietarioId).SetIsRequired(true);
            cm.MapMember(x => x.ConvidadoId).SetIsRequired(true);
            cm.MapMember(x => x.ConvidadoEmail).SetIsRequired(true);
            cm.MapMember(x => x.Permissao).SetIsRequired(true);
            cm.MapMember(x => x.Status).SetIsRequired(true);
        });
    }
}
