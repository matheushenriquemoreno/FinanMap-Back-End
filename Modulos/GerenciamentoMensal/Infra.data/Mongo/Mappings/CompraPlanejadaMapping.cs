using Domain.Entity;
using Infra.Configure.Env;
using Infra.Data.Mongo.Config.Interface;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Mappings;

public class CompraPlanejadaMapping : IMongoMapping
{
    public void RegisterMap(IMongoClient mongoClient)
    {
        BsonClassMap.TryRegisterClassMap<CompraPlanejada>(classMap =>
        {
            classMap.AutoMap();
            classMap.SetIgnoreExtraElements(true);
            classMap.MapMember(compra => compra.UsuarioId)
                .SetSerializer(new StringSerializer(BsonType.ObjectId));
            classMap.MapMember(compra => compra.DespesaId)
                .SetSerializer(new StringSerializer(BsonType.ObjectId))
                .SetIgnoreIfNull(true);
        });

        BsonClassMap.TryRegisterClassMap<LinkLojaCompraPlanejada>(classMap =>
        {
            classMap.AutoMap();
            classMap.SetIgnoreExtraElements(true);
        });

        var collection = mongoClient
            .GetDatabase(MongoDBSettings.DataBaseName)
            .GetCollection<CompraPlanejada>("ComprasPlanejadas");

        var indexKeys = Builders<CompraPlanejada>.IndexKeys
            .Ascending(compra => compra.UsuarioId)
            .Ascending(compra => compra.Estado)
            .Descending(compra => compra.Prioridade)
            .Descending(compra => compra.DataCriacao);

        collection.Indexes.CreateOne(new CreateIndexModel<CompraPlanejada>(
            indexKeys,
            new CreateIndexOptions<CompraPlanejada>
            {
                Background = true,
                Name = "ix_compras_planejadas_contexto_estado_prioridade_data"
            }));
    }
}
