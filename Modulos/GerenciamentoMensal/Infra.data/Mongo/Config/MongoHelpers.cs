using Infra.Configure.Env;
using MongoDB.Driver;

namespace Infra.Data.Mongo;

public static class MongoHelpers
{
    public static IMongoCollection<T> GetCollection<T>(this IMongoClient mongoClient, string database, string collectionName = null)
    {
        if(mongoClient == null)
            collectionName = nameof(T);

        var mongoDatabase = mongoClient.GetDatabase(MongoDBSettings.DataBaseName);

        return mongoDatabase.GetCollection<T>(collectionName);
    }
}
