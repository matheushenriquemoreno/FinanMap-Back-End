using Domain.Entity;
using Infra.Data.Mongo.Config.Interface;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Mappings;

public class UsuarioMapping : IMongoMapping
{
    public void RegisterMap(IMongoClient mongoClient)
    {
        BsonClassMap.TryRegisterClassMap<Usuario>(cm =>
        {
            cm.AutoMap();
            cm.MapMember(x => x.Email).SetIsRequired(true);
            cm.MapMember(x => x.Nome).SetIsRequired(true);
        });
    }
}
