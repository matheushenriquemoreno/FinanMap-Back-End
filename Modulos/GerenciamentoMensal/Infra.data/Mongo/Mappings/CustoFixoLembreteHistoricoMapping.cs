using Domain.Entity;
using Infra.Configure.Env;
using Infra.Data.Mongo.Config.Interface;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Mappings;

public class CustoFixoLembreteHistoricoMapping : IMongoMapping
{
    public void RegisterMap(IMongoClient mongoClient)
    {
        BsonClassMap.TryRegisterClassMap<CustoFixoLembreteHistorico>(classMap =>
        {
            classMap.AutoMap();
            classMap.MapMember(m => m.UsuarioId).SetSerializer(new StringSerializer(BsonType.ObjectId));
        });

        var mongoDatabase = mongoClient.GetDatabase(MongoDBSettings.DataBaseName);
        IMongoCollection<CustoFixoLembreteHistorico> mongoCollection = mongoDatabase.GetCollection<CustoFixoLembreteHistorico>("CustosFixosLembretesHistorico");

        CreateIndexes(mongoCollection);
    }

    private void CreateIndexes(IMongoCollection<CustoFixoLembreteHistorico> mongoCollection)
    {
        // 1. Índice composto único: { UsuarioId: 1, DataReferencia: 1, TipoLembrete: 1 }
        var uniqueKeys = Builders<CustoFixoLembreteHistorico>.IndexKeys
            .Ascending(x => x.UsuarioId)
            .Ascending(x => x.DataReferencia)
            .Ascending(x => x.TipoLembrete);

        var uniqueOptions = new CreateIndexOptions<CustoFixoLembreteHistorico>
        {
            Unique = true,
            Background = true
        };

        var uniqueModel = new CreateIndexModel<CustoFixoLembreteHistorico>(uniqueKeys, uniqueOptions);
        mongoCollection.Indexes.CreateOne(uniqueModel);

        // 2. Índice TTL em CreatedAt: expira após 60 dias
        var ttlKeys = Builders<CustoFixoLembreteHistorico>.IndexKeys.Ascending(x => x.CreatedAt);
        var ttlOptions = new CreateIndexOptions
        {
            ExpireAfter = TimeSpan.FromDays(60),
            Background = true
        };

        var ttlModel = new CreateIndexModel<CustoFixoLembreteHistorico>(ttlKeys, ttlOptions);
        mongoCollection.Indexes.CreateOne(ttlModel);
    }
}
