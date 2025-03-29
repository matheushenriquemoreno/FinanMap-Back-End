using Domain.Entity;
using Infra.Configure.Env;
using Infra.Data.Mongo.Config.Interface;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Mappings;

internal class RendimentoMapping : IMongoMapping
{
    public void RegisterMap(IMongoClient mongoClient)
    {
        BsonClassMap.TryRegisterClassMap<Rendimento>(cm =>
        {
            cm.AutoMap();
        });

        var mongoDatabase = mongoClient.GetDatabase(MongoDBSettings.DataBaseName);
        var mongoCollection = mongoDatabase.GetCollection<Rendimento>(nameof(Rendimento));
        TransacaoMapping.CreateIndexIfNotExists(mongoCollection);
    }
}
