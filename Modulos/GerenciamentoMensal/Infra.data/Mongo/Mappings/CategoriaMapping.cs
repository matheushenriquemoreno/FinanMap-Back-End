using Domain.Entity;
using Infra.Configure.Env;
using Infra.Data.Mongo.Config.Interface;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Mappings;

public class CategoriaMapping : IMongoMapping
{
    public void RegisterMap(IMongoClient mongoClient)
    {
        BsonClassMap.TryRegisterClassMap<Categoria>(classMap =>
        {
            classMap.AutoMap();
            classMap.MapMember(m => m.UsuarioId).SetSerializer(new StringSerializer(BsonType.ObjectId));
        });

        var mongoDatabase = mongoClient.GetDatabase(MongoDBSettings.DataBaseName);
        IMongoCollection<Categoria> mongoCollection = mongoDatabase.GetCollection<Categoria>(nameof(Categoria));

        CreateIndex(mongoCollection);
    }

    private void CreateIndex(IMongoCollection<Categoria> mongoCollection)
    {
        var indexKeysDefinition = Builders<Categoria>.IndexKeys
             .Ascending(x => x.Tipo)
             .Ascending(x => x.UsuarioId)
                .Ascending(x => x.Nome);

        var createIndexOptions = new CreateIndexOptions { Background = true };

        var createIndexModel = new CreateIndexModel<Categoria>(indexKeysDefinition, createIndexOptions);

        mongoCollection.Indexes.CreateOne(createIndexModel);
    }

}
