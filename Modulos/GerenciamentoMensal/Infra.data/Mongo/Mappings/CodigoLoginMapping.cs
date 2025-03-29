using Domain.Entity;
using Domain.Login.Entity;
using Infra.Configure.Env;
using Infra.Data.Mongo.Config.Interface;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Mappings;

public class CodigoLoginMapping : IMongoMapping
{
    public void RegisterMap(IMongoClient mongoClient)
    {
        BsonClassMap.TryRegisterClassMap<CodigoLogin>(cm =>
        {
            cm.AutoMap();
            cm.MapMember(x => x.Email).SetIsRequired(true);
            cm.MapMember(x => x.Codigo).SetIsRequired(true);
            cm.MapMember(x => x.DataCriacao).SetSerializer(new DateTimeSerializer(DateTimeKind.Utc, BsonType.DateTime));
            cm.MapMember(x => x.DataExpiracao).SetSerializer(new DateTimeSerializer(DateTimeKind.Utc, BsonType.DateTime));
        });

        var mongoDatabase = mongoClient.GetDatabase(MongoDBSettings.DataBaseName);
        IMongoCollection<CodigoLogin> mongoCollection = mongoDatabase.GetCollection<CodigoLogin>(nameof(CodigoLogin));
        CriarIndex(mongoCollection);
    }

    private void CriarIndex(IMongoCollection<CodigoLogin> mongoCollection)
    {
        var indexDefinicaoCodigo = Builders<CodigoLogin>.IndexKeys
            .Ascending(x => x.Codigo);

        var indexDefinicaoEmail = Builders<CodigoLogin>.IndexKeys
            .Ascending(x => x.Email);

        var createIndexOptions = new CreateIndexOptions { Background = true };

        var createIndexModelCodigo = new CreateIndexModel<CodigoLogin>(indexDefinicaoCodigo, createIndexOptions);
        var createIndexModelEmail = new CreateIndexModel<CodigoLogin>(indexDefinicaoEmail, createIndexOptions);

        mongoCollection.Indexes.CreateOne(createIndexModelCodigo);
        mongoCollection.Indexes.CreateOne(createIndexModelEmail);
    }
}
