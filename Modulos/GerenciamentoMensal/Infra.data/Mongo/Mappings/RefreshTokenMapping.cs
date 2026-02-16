using Domain.Login.Entity;
using Infra.Configure.Env;
using Infra.Data.Mongo.Config.Interface;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Mappings;

public class RefreshTokenMapping : IMongoMapping
{
    public void RegisterMap(IMongoClient mongoClient)
    {
        BsonClassMap.TryRegisterClassMap<RefreshToken>(cm =>
        {
            cm.AutoMap();
            cm.MapMember(x => x.Token).SetIsRequired(true);
            cm.MapMember(x => x.UsuarioId).SetIsRequired(true);
            cm.MapMember(x => x.DataCriacao).SetSerializer(new DateTimeSerializer(DateTimeKind.Utc, BsonType.DateTime));
            cm.MapMember(x => x.DataExpiracao).SetSerializer(new DateTimeSerializer(DateTimeKind.Utc, BsonType.DateTime));
        });

        var mongoDatabase = mongoClient.GetDatabase(MongoDBSettings.DataBaseName);
        IMongoCollection<RefreshToken> mongoCollection = mongoDatabase.GetCollection<RefreshToken>(nameof(RefreshToken));
        CriarIndex(mongoCollection);
    }

    private void CriarIndex(IMongoCollection<RefreshToken> mongoCollection)
    {
        var indexDefinicaoToken = Builders<RefreshToken>.IndexKeys
            .Ascending(x => x.Token);

        var indexDefinicaoUsuarioId = Builders<RefreshToken>.IndexKeys
            .Ascending(x => x.UsuarioId);

        var createIndexOptions = new CreateIndexOptions { Background = true };

        var createIndexModelToken = new CreateIndexModel<RefreshToken>(indexDefinicaoToken, createIndexOptions);
        var createIndexModelUsuarioId = new CreateIndexModel<RefreshToken>(indexDefinicaoUsuarioId, createIndexOptions);

        mongoCollection.Indexes.CreateOne(createIndexModelToken);
        mongoCollection.Indexes.CreateOne(createIndexModelUsuarioId);
    }
}
