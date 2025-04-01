using Infra.Configure.Env;
using MongoDB.Driver;
using SharedDomain.Entity;

namespace Infra.Data.Mongo;

public static class MongoHelpers
{
    public static IMongoCollection<T> GetCollection<T>(this IMongoClient mongoClient, string database, string collectionName = null) where T : IEntityBase
    {
        if (collectionName == null)
            collectionName = typeof(T).Name;

        var mongoDatabase = mongoClient.GetDatabase(MongoDBSettings.DataBaseName);

        return mongoDatabase.GetCollection<T>(collectionName);
    }
}
