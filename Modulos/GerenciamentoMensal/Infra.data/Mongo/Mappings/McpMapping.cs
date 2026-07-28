using Domain.Mcp.Entities;
using Infra.Data.Mongo.Config.Interface;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Mappings;

public sealed class McpMapping : IMongoMapping
{
    public void RegisterMap(IMongoClient mongoClient)
    {
        new EntityBaseMapping().RegisterMap(mongoClient);
        BsonClassMap.TryRegisterClassMap<McpConnection>(map => map.AutoMap());
        BsonClassMap.TryRegisterClassMap<McpAuthorizationInteraction>(map => map.AutoMap());
        BsonClassMap.TryRegisterClassMap<McpOperationJournal>(map =>
        {
            map.AutoMap();
            map.GetMemberMap(item => item.PreviewId).SetIgnoreIfNull(true);
        });
        BsonClassMap.TryRegisterClassMap<McpOperationStep>(map => map.AutoMap());
        BsonClassMap.TryRegisterClassMap<McpPreview>(map => map.AutoMap());
        BsonClassMap.TryRegisterClassMap<McpImportBatch>(map =>
        {
            map.AutoMap();
            map.GetMemberMap(item => item.ParentBatchId).SetIgnoreIfNull(true);
        });
        BsonClassMap.TryRegisterClassMap<McpImportSourceRef>(map => map.AutoMap());
        BsonClassMap.TryRegisterClassMap<McpImportItemError>(map => map.AutoMap());
        BsonClassMap.TryRegisterClassMap<McpImportItem>(map =>
        {
            map.AutoMap();
            map.GetMemberMap(item => item.SourceRef).SetIgnoreIfNull(true);
            map.GetMemberMap(item => item.DuplicateDecision).SetIgnoreIfNull(true);
            map.GetMemberMap(item => item.OperationId).SetIgnoreIfNull(true);
            map.GetMemberMap(item => item.CreatedEntityId).SetIgnoreIfNull(true);
            map.GetMemberMap(item => item.FinishedAtUtc).SetIgnoreIfNull(true);
        });

        var database = mongoClient.GetDatabase();
        CreateConnectionIndexes(database.GetCollection<McpConnection>("McpConnections"));
        CreateInteractionIndexes(database.GetCollection<McpAuthorizationInteraction>("McpAuthorizationInteractions"));
        CreateJournalIndexes(database.GetCollection<McpOperationJournal>("McpOperationJournal"));
        CreatePreviewIndexes(database.GetCollection<McpPreview>("McpPreviews"));
        CreateImportBatchIndexes(
            database.GetCollection<McpImportBatch>("McpImportBatches"));
        CreateImportItemIndexes(
            database.GetCollection<McpImportItem>("McpImportItems"));
        CreateFinancialEffectIndexes(database.GetCollection<BsonDocument>("Categoria"));
        CreateFinancialEffectIndexes(database.GetCollection<BsonDocument>("Rendimento"));
        CreateFinancialEffectIndexes(database.GetCollection<BsonDocument>("Despesa"));
        CreateFinancialEffectIndexes(database.GetCollection<BsonDocument>("Investimento"));
        CreateFinancialEffectIndexes(database.GetCollection<BsonDocument>("CustosFixos"));
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
                    .Ascending(item => item.NextAttemptAtUtc)),
            new CreateIndexModel<McpOperationJournal>(
                Builders<McpOperationJournal>.IndexKeys.Ascending(item => item.PreviewId),
                new CreateIndexOptions<McpOperationJournal>
                {
                    Unique = true,
                    PartialFilterExpression =
                        Builders<McpOperationJournal>.Filter.Type(
                            item => item.PreviewId,
                            BsonType.String)
                }),
            new CreateIndexModel<McpOperationJournal>(
                Builders<McpOperationJournal>.IndexKeys
                    .Ascending(item => item.UserId)
                    .Ascending(item => item.ConnectionId)
                    .Ascending(item => item.ToolName)
                    .Ascending(item => item.IdempotencyKey),
                new CreateIndexOptions<McpOperationJournal>
                {
                    Unique = true,
                    PartialFilterExpression =
                        Builders<McpOperationJournal>.Filter.Type(
                            item => item.IdempotencyKey,
                            MongoDB.Bson.BsonType.String)
                })
        ]);
    }

    private static void CreatePreviewIndexes(IMongoCollection<McpPreview> collection)
    {
        collection.Indexes.CreateMany(
        [
            new CreateIndexModel<McpPreview>(
                Builders<McpPreview>.IndexKeys
                    .Ascending(item => item.UserId)
                    .Ascending(item => item.ConnectionId)
                    .Ascending(item => item.ToolName)
                    .Ascending(item => item.RequestId),
                new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<McpPreview>(
                Builders<McpPreview>.IndexKeys
                    .Ascending(item => item.UserId)
                    .Ascending(item => item.State)
                    .Ascending(item => item.ExpiresAtUtc)),
            new CreateIndexModel<McpPreview>(
                Builders<McpPreview>.IndexKeys.Ascending(item => item.PurgeAtUtc),
                new CreateIndexOptions
                {
                    ExpireAfter = TimeSpan.Zero
                })
        ]);
    }

    private static void CreateFinancialEffectIndexes(
        IMongoCollection<BsonDocument> collection)
    {
        var keys = Builders<BsonDocument>.IndexKeys;
        var filter = Builders<BsonDocument>.Filter;
        collection.Indexes.CreateMany(
        [
            new CreateIndexModel<BsonDocument>(
                keys.Ascending("UsuarioId").Ascending("McpOperationId"),
                new CreateIndexOptions<BsonDocument>
                {
                    Unique = true,
                    PartialFilterExpression =
                        filter.Type("McpOperationId", BsonType.String)
                }),
            new CreateIndexModel<BsonDocument>(
                keys.Ascending("UsuarioId").Ascending("LastMcpOperationId"),
                new CreateIndexOptions<BsonDocument>
                {
                    PartialFilterExpression =
                        filter.Type("LastMcpOperationId", BsonType.String)
                })
        ]);
    }

    private static void CreateImportBatchIndexes(
        IMongoCollection<McpImportBatch> collection)
    {
        collection.Indexes.CreateMany(
        [
            new CreateIndexModel<McpImportBatch>(
                Builders<McpImportBatch>.IndexKeys
                    .Ascending(item => item.UserId)
                    .Ascending(item => item.ConnectionId)
                    .Ascending(item => item.BatchKey),
                new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<McpImportBatch>(
                Builders<McpImportBatch>.IndexKeys
                    .Ascending(item => item.UserId)
                    .Descending(item => item.CreatedAtUtc)),
            new CreateIndexModel<McpImportBatch>(
                Builders<McpImportBatch>.IndexKeys
                    .Ascending(item => item.State)
                    .Ascending(item => item.CreatedAtUtc)),
            new CreateIndexModel<McpImportBatch>(
                Builders<McpImportBatch>.IndexKeys
                    .Ascending(item => item.PurgeAtUtc),
                new CreateIndexOptions { ExpireAfter = TimeSpan.Zero })
        ]);
    }

    private static void CreateImportItemIndexes(
        IMongoCollection<McpImportItem> collection)
    {
        collection.Indexes.CreateMany(
        [
            new CreateIndexModel<McpImportItem>(
                Builders<McpImportItem>.IndexKeys
                    .Ascending(item => item.BatchId)
                    .Ascending(item => item.ClientItemId),
                new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<McpImportItem>(
                Builders<McpImportItem>.IndexKeys
                    .Ascending(item => item.BatchId)
                    .Ascending(item => item.ValidationState)),
            new CreateIndexModel<McpImportItem>(
                Builders<McpImportItem>.IndexKeys
                    .Ascending(item => item.UserId)
                    .Ascending(item => item.Fingerprint)),
            new CreateIndexModel<McpImportItem>(
                Builders<McpImportItem>.IndexKeys
                    .Ascending(item => item.PurgeAtUtc),
                new CreateIndexOptions { ExpireAfter = TimeSpan.Zero })
        ]);
    }
}
