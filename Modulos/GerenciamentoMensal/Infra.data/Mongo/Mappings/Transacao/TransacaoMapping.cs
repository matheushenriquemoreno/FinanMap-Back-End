using Domain.Entity;
using Infra.Data.Mongo.Config.Interface;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Mappings;

internal class TransacaoMapping : IMongoMappingClassBase
{
    public void RegisterMap(IMongoClient mongoClient)
    {
        BsonClassMap.TryRegisterClassMap<Transacao>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.UnmapMember(m => m.Categoria);
            cm.UnmapMember(m => m.Usuario);
            cm.MapMember(m => m.CategoriaId).SetSerializer(new StringSerializer(BsonType.ObjectId));
            cm.MapMember(m => m.UsuarioId).SetSerializer(new StringSerializer(BsonType.ObjectId));
        });
    }

    internal static void CreateIndexIfNotExists<T>(IMongoCollection<T> collection) where T : Transacao
    {
        try
        {
            var indexKeysDefinition = Builders<T>.IndexKeys
             .Ascending(x => x.Ano)
             .Ascending(x => x.Mes)
             .Ascending(x => x.UsuarioId);

            var createIndexOptions = new CreateIndexOptions { Background = true };
            var createIndexModel = new CreateIndexModel<T>(indexKeysDefinition, createIndexOptions);

            collection.Indexes.CreateOne(createIndexModel);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }
}
