using Domain.Mcp.Entities;
using Infra.Data.Mongo.Config.Interface;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Mappings;

public sealed class McpMapping : IMongoMapping
{
    public void RegisterMap(IMongoClient mongoClient)
    {
        BsonClassMap.TryRegisterClassMap<McpConnection>(map => map.AutoMap());
        BsonClassMap.TryRegisterClassMap<McpAuthorizationInteraction>(map => map.AutoMap());
        BsonClassMap.TryRegisterClassMap<McpOperationJournal>(map => map.AutoMap());

        var database = mongoClient.GetDatabase();
        CreateConnectionIndexes(database.GetCollection<McpConnection>("McpConnections"));
        CreateInteractionIndexes(database.GetCollection<McpAuthorizationInteraction>("McpAuthorizationInteractions"));
        CreateJournalIndexes(database.GetCollection<McpOperationJournal>("McpOperationJournal"));
    }

    private static void CreateConnectionIndexes(IMongoCollection<McpConnection> collection)
    {
        collection.Indexes.CreateMany(
        [
            new CreateIndexModel<McpConnection>(
                Builders<McpConnection>.IndexKeys.Ascending(item => item.AuthorizationId),
                new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<McpConnection>(
                Builders<McpConnection>.IndexKeys
                    .Ascending(item => item.UserId)
                    .Ascending(item => item.Status)
                    .Descending(item => item.CreatedAtUtc)),
            new CreateIndexModel<McpConnection>(
                Builders<McpConnection>.IndexKeys
                    .Ascending(item => item.ClientId)
                    .Ascending(item => item.UserId))
        ]);
    }

    private static void CreateInteractionIndexes(IMongoCollection<McpAuthorizationInteraction> collection)
    {
        collection.Indexes.CreateOne(new CreateIndexModel<McpAuthorizationInteraction>(
            Builders<McpAuthorizationInteraction>.IndexKeys.Ascending(item => item.ExpiresAtUtc)));
    }

    private static void CreateJournalIndexes(IMongoCollection<McpOperationJournal> collection)
    {
        collection.Indexes.CreateMany(
        [
            new CreateIndexModel<McpOperationJournal>(
                Builders<McpOperationJournal>.IndexKeys
                    .Ascending(item => item.UserId)
                    .Descending(item => item.StartedAtUtc)),
            new CreateIndexModel<McpOperationJournal>(
                Builders<McpOperationJournal>.IndexKeys.Ascending(item => item.CorrelationId)),
            new CreateIndexModel<McpOperationJournal>(
                Builders<McpOperationJournal>.IndexKeys
                    .Ascending(item => item.State)
                    .Ascending(item => item.StartedAtUtc))
        ]);
    }
}
