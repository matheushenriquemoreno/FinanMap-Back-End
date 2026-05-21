using Domain.Entity;
using Infra.Configure.Env;
using Infra.Data.Mongo.Config.Interface;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Mappings;

public class CustoFixoMapping : IMongoMapping
{
    public void RegisterMap(IMongoClient mongoClient)
    {
        BsonClassMap.TryRegisterClassMap<CustoFixo>(classMap =>
        {
            classMap.AutoMap();
            classMap.MapMember(m => m.UsuarioId).SetSerializer(new StringSerializer(BsonType.ObjectId));
            classMap.MapMember(m => m.CategoriaId).SetSerializer(new StringSerializer(BsonType.ObjectId));
        });

        var mongoDatabase = mongoClient.GetDatabase(MongoDBSettings.DataBaseName);
        IMongoCollection<CustoFixo> mongoCollection = mongoDatabase.GetCollection<CustoFixo>("CustosFixos");

        CreateIndex(mongoCollection);
    }

    private void CreateIndex(IMongoCollection<CustoFixo> mongoCollection)
    {
        var indexKeysDefinition = Builders<CustoFixo>.IndexKeys
            .Ascending(x => x.UsuarioId)
            .Ascending(x => x.Nome)
            .Ascending(x => x.DiaVencimento);

        var createIndexOptions = new CreateIndexOptions<CustoFixo>
        {
            Background = true,
            Unique = true,
            PartialFilterExpression = Builders<CustoFixo>.Filter.Eq(x => x.Ativo, true)
        };

        var createIndexModel = new CreateIndexModel<CustoFixo>(indexKeysDefinition, createIndexOptions);

        mongoCollection.Indexes.CreateOne(createIndexModel);
    }
}
