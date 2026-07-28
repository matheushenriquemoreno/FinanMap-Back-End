#nullable enable

using Application.Mcp.Models;
using Domain.Mcp.Entities;
using Domain.Mcp.Enums;
using Infra.Data.Mongo.Mappings;
using Infra.Data.Mongo.Mcp;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Tests;

public sealed class McpImportPersistencePhase5UnitTests
{
    [Fact]
    public void Batch_and_item_preserve_owner_source_validation_result_and_operation_identity()
    {
        var now = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
        var batch = McpImportBatch.Create(
            "owner-a",
            "connection-a",
            "batch-key-a",
            parentBatchId: null,
            now);
        var item = McpImportItem.Create(
            batch.Id,
            batch.UserId,
            "expense-row-2",
            new McpImportSourceRef("Despesas", 2, "A2:H2"),
            McpImportItemType.Expense,
            [1, 2, 3],
            "fingerprint-a",
            McpImportValidationState.Valid,
            [],
            now.AddHours(24));

        item.RecordResult(
            "operation-a",
            "expense-a",
            now.AddMinutes(1));

        Assert.Equal(McpImportBatchState.Preparing, batch.State);
        Assert.Equal(0, batch.Version);
        Assert.Equal("owner-a", batch.UserId);
        Assert.Equal("connection-a", batch.ConnectionId);
        Assert.Equal("batch-key-a", batch.BatchKey);
        Assert.Equal(now.AddHours(24), batch.PurgeAtUtc);
        Assert.Equal(batch.Id, item.BatchId);
        Assert.Equal(batch.UserId, item.UserId);
        Assert.Equal("expense-row-2", item.ClientItemId);
        Assert.Equal("Despesas", item.SourceRef!.Sheet);
        Assert.Equal(2, item.SourceRef.Row);
        Assert.Equal(McpImportValidationState.Valid, item.ValidationState);
        Assert.Equal("operation-a", item.OperationId);
        Assert.Equal("expense-a", item.CreatedEntityId);
        Assert.Equal(now.AddMinutes(1), item.FinishedAtUtc);
        Assert.Equal(1, item.Version);
        Assert.Empty(item.NormalizedDataCiphertext);
    }

    [Fact]
    public void Batch_state_transitions_keep_aggregates_and_increment_the_cas_version()
    {
        var now = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
        var batch = McpImportBatch.Create(
            "owner-a",
            "connection-a",
            "batch-key-a",
            null,
            now);

        batch.MarkPrepared(
            new Dictionary<string, int> { ["valid"] = 2, ["invalid"] = 1 },
            new Dictionary<string, decimal> { ["expense"] = 25.50m });
        batch.StartProcessing();
        batch.Finish(
            McpImportBatchState.Partial,
            new Dictionary<string, int> { ["completed"] = 1, ["failed"] = 1 },
            new Dictionary<string, decimal> { ["expense"] = 10m },
            now.AddMinutes(2));

        Assert.Equal(McpImportBatchState.Partial, batch.State);
        Assert.Equal(1, batch.Counts["completed"]);
        Assert.Equal(10m, batch.Totals["expense"]);
        Assert.Equal(now.AddMinutes(2), batch.FinishedAtUtc);
        Assert.Equal(3, batch.Version);
    }

    [Fact]
    public void Expired_processing_lease_can_be_reacquired_but_live_lease_cannot()
    {
        var now = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
        var batch = McpImportBatch.Create(
            "owner-a",
            "connection-a",
            "lease-batch",
            null,
            now);
        batch.MarkPrepared(
            new Dictionary<string, int>(),
            new Dictionary<string, decimal>());
        batch.StartProcessing("correlation-a", "2025-11-25", "client-a");

        Assert.True(batch.TryAcquireProcessingLease(
            "worker-a",
            now,
            TimeSpan.FromMinutes(1)));
        Assert.False(batch.TryAcquireProcessingLease(
            "worker-b",
            now.AddSeconds(30),
            TimeSpan.FromMinutes(1)));
        Assert.True(batch.TryAcquireProcessingLease(
            "worker-b",
            now.AddMinutes(2),
            TimeSpan.FromMinutes(1)));
        Assert.Equal("worker-b", batch.ProcessingLeaseOwner);
        Assert.Equal("correlation-a", batch.ConfirmationCorrelationId);
        Assert.Equal("client-a", batch.ConfirmationClientId);
    }
}

[Collection(McpMongoCollection.Name)]
public sealed class McpImportPersistencePhase5MongoTests(McpMongoFixture mongo)
{
    [Fact]
    public async Task Legacy_duplicate_lookup_is_owner_type_period_amount_category_and_description_scoped()
    {
        var client = new MongoClient(mongo.ConnectionString);
        var database = client.GetDatabase("FinanMap");
        var expenses = database.GetCollection<BsonDocument>("Despesa");
        var suffix = Guid.NewGuid().ToString("N");
        var owner = $"owner-{suffix}";
        var otherOwner = $"other-{suffix}";
        var categoryId = $"category-{suffix}";
        var store = new McpWriteEffectStore(client);
        var command = new McpWriteCommand(
            McpWriteEntity.Expense,
            McpPreviewAction.Create,
            null,
            new Dictionary<string, object?>
            {
                ["year"] = 2026,
                ["month"] = 7,
                ["description"] = "Taxi",
                ["amount"] = "50.00",
                ["categoryId"] = categoryId
            });
        await expenses.InsertManyAsync(
        [
            new BsonDocument
            {
                ["_id"] = ObjectId.GenerateNewId(),
                ["UsuarioId"] = owner,
                ["Ano"] = 2026,
                ["Mes"] = 7,
                ["Descricao"] = "Táxi",
                ["Valor"] = 50.00m,
                ["CategoriaId"] = categoryId
            },
            new BsonDocument
            {
                ["_id"] = ObjectId.GenerateNewId(),
                ["UsuarioId"] = otherOwner,
                ["Ano"] = 2026,
                ["Mes"] = 7,
                ["Descricao"] = "Táxi",
                ["Valor"] = 50.00m,
                ["CategoriaId"] = categoryId
            }
        ]);

        try
        {
            Assert.True(await store.HasPossibleDuplicateAsync(owner, command));
            Assert.False(await store.HasPossibleDuplicateAsync(
                owner,
                command with
                {
                    Values = new Dictionary<string, object?>(command.Values)
                    {
                        ["amount"] = "50.01"
                    }
                }));
            Assert.False(await store.HasPossibleDuplicateAsync(
                $"missing-{suffix}",
                command));
        }
        finally
        {
            await expenses.DeleteManyAsync(
                Builders<BsonDocument>.Filter.In(
                    "UsuarioId",
                    new[] { owner, otherOwner }));
        }
    }

    [Fact]
    public async Task Repository_isolates_owner_and_batch_and_finds_only_owned_fingerprints()
    {
        var client = new MongoClient(mongo.ConnectionString);
        new McpMapping().RegisterMap(client);
        var repository = new McpImportRepository(client);
        var suffix = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var ownerA = $"owner-a-{suffix}";
        var ownerB = $"owner-b-{suffix}";
        var batchA = McpImportBatch.Create(
            ownerA,
            $"connection-a-{suffix}",
            $"batch-a-{suffix}",
            null,
            now);
        var batchB = McpImportBatch.Create(
            ownerB,
            $"connection-b-{suffix}",
            $"batch-b-{suffix}",
            null,
            now);
        var fingerprint = $"fingerprint-{suffix}";
        var itemA = McpImportItem.Create(
            batchA.Id,
            ownerA,
            "row-1",
            new McpImportSourceRef("Aba A", 1, null),
            McpImportItemType.Income,
            [1],
            fingerprint,
            McpImportValidationState.Valid,
            [],
            now.AddHours(24));
        var itemB = McpImportItem.Create(
            batchB.Id,
            ownerB,
            "row-1",
            new McpImportSourceRef("Aba B", 1, null),
            McpImportItemType.Income,
            [2],
            fingerprint,
            McpImportValidationState.Valid,
            [],
            now.AddHours(24));

        try
        {
            Assert.True((await repository.CreateOrGetBatchAsync(batchA)).Created);
            Assert.True((await repository.CreateOrGetBatchAsync(batchB)).Created);
            await repository.AddItemsAsync([itemA, itemB]);

            Assert.Equal(
                batchA.Id,
                (await repository.GetOwnedBatchAsync(
                    batchA.Id,
                    ownerA,
                    batchA.ConnectionId))!.Id);
            Assert.Null(await repository.GetOwnedBatchAsync(
                batchA.Id,
                ownerB,
                batchA.ConnectionId));
            Assert.Equal(
                itemA.Id,
                (await repository.GetOwnedItemAsync(
                    batchA.Id,
                    itemA.ClientItemId,
                    ownerA))!.Id);
            Assert.Null(await repository.GetOwnedItemAsync(
                batchA.Id,
                itemA.ClientItemId,
                ownerB));
            Assert.Equal(itemA.Id, Assert.Single(
                await repository.ListOwnedItemsAsync(batchA.Id, ownerA)).Id);
            Assert.Equal(itemA.Id, Assert.Single(
                await repository.FindOwnedByFingerprintAsync(
                    ownerA,
                    fingerprint,
                    10)).Id);
        }
        finally
        {
            await CleanupAsync(client, ownerA, ownerB);
        }
    }

    [Fact]
    public async Task Mongo_indexes_enforce_batch_key_and_client_item_identity_and_publish_fingerprint_and_ttl()
    {
        var client = new MongoClient(mongo.ConnectionString);
        new McpMapping().RegisterMap(client);
        var database = client.GetDatabase("FinanMap");
        var batches = database.GetCollection<McpImportBatch>("McpImportBatches");
        var items = database.GetCollection<McpImportItem>("McpImportItems");
        var suffix = Guid.NewGuid().ToString("N");
        var owner = $"owner-{suffix}";
        var connection = $"connection-{suffix}";
        var now = DateTime.UtcNow;
        var firstBatch = McpImportBatch.Create(
            owner,
            connection,
            $"batch-{suffix}",
            null,
            now);
        var duplicateBatchKey = McpImportBatch.Create(
            owner,
            connection,
            firstBatch.BatchKey,
            null,
            now);
        var secondBatch = McpImportBatch.Create(
            owner,
            connection,
            $"batch-2-{suffix}",
            null,
            now);
        var firstItem = McpImportItem.Create(
            firstBatch.Id,
            owner,
            "row-1",
            null,
            McpImportItemType.Category,
            [1],
            $"fingerprint-{suffix}",
            McpImportValidationState.Valid,
            [],
            now.AddHours(24));
        var duplicateClientItemId = McpImportItem.Create(
            firstBatch.Id,
            owner,
            firstItem.ClientItemId,
            null,
            McpImportItemType.Category,
            [2],
            $"fingerprint-2-{suffix}",
            McpImportValidationState.Valid,
            [],
            now.AddHours(24));
        var sameClientItemInAnotherBatch = McpImportItem.Create(
            secondBatch.Id,
            owner,
            firstItem.ClientItemId,
            null,
            McpImportItemType.Category,
            [3],
            $"fingerprint-3-{suffix}",
            McpImportValidationState.Valid,
            [],
            now.AddHours(24));

        try
        {
            await batches.InsertOneAsync(firstBatch);
            await Assert.ThrowsAsync<MongoWriteException>(() =>
                batches.InsertOneAsync(duplicateBatchKey));
            await batches.InsertOneAsync(secondBatch);
            await items.InsertOneAsync(firstItem);
            await Assert.ThrowsAsync<MongoWriteException>(() =>
                items.InsertOneAsync(duplicateClientItemId));
            await items.InsertOneAsync(sameClientItemInAnotherBatch);

            var batchIndexes = await (await batches.Indexes.ListAsync()).ToListAsync();
            var batchIdentity = Assert.Single(batchIndexes, index =>
                index.GetValue("name", "").AsString ==
                "UserId_1_ConnectionId_1_BatchKey_1");
            Assert.True(batchIdentity.GetValue("unique", false).ToBoolean());
            var batchTtl = Assert.Single(batchIndexes, index =>
                index.GetValue("name", "").AsString == "PurgeAtUtc_1");
            Assert.Equal(0, batchTtl["expireAfterSeconds"].ToInt32());

            var itemIndexes = await (await items.Indexes.ListAsync()).ToListAsync();
            var itemIdentity = Assert.Single(itemIndexes, index =>
                index.GetValue("name", "").AsString ==
                "BatchId_1_ClientItemId_1");
            Assert.True(itemIdentity.GetValue("unique", false).ToBoolean());
            Assert.Contains(itemIndexes, index =>
                index.GetValue("name", "").AsString ==
                "UserId_1_Fingerprint_1");
            var ttl = Assert.Single(itemIndexes, index =>
                index.GetValue("name", "").AsString == "PurgeAtUtc_1");
            Assert.Equal(0, ttl["expireAfterSeconds"].ToInt32());
        }
        finally
        {
            await CleanupAsync(client, owner);
        }
    }

    [Fact]
    public async Task Repository_replaces_batch_and_item_with_owner_scoped_compare_and_set()
    {
        var client = new MongoClient(mongo.ConnectionString);
        new McpMapping().RegisterMap(client);
        var repository = new McpImportRepository(client);
        var suffix = Guid.NewGuid().ToString("N");
        var owner = $"owner-{suffix}";
        var now = DateTime.UtcNow;
        var batch = McpImportBatch.Create(
            owner,
            $"connection-{suffix}",
            $"batch-{suffix}",
            null,
            now);
        var item = McpImportItem.Create(
            batch.Id,
            owner,
            "row-1",
            null,
            McpImportItemType.Expense,
            [1],
            $"fingerprint-{suffix}",
            McpImportValidationState.Valid,
            [],
            now.AddHours(24),
            now);

        try
        {
            await repository.CreateOrGetBatchAsync(batch);
            await repository.AddItemsAsync([item]);
            batch.MarkPrepared(
                new Dictionary<string, int> { ["valid"] = 1 },
                new Dictionary<string, decimal> { ["expense"] = 20m });
            item.RecordResult("operation-a", "expense-a", now.AddMinutes(1));

            Assert.False(await repository.ReplaceBatchAsync(
                batch,
                $"other-{suffix}",
                expectedVersion: 0));
            Assert.True(await repository.ReplaceBatchAsync(
                batch,
                owner,
                expectedVersion: 0));
            Assert.False(await repository.ReplaceBatchAsync(
                batch,
                owner,
                expectedVersion: 0));
            Assert.False(await repository.ReplaceItemAsync(
                item,
                $"other-{suffix}",
                expectedVersion: 0));
            Assert.True(await repository.ReplaceItemAsync(
                item,
                owner,
                expectedVersion: 0));
            Assert.False(await repository.ReplaceItemAsync(
                item,
                owner,
                expectedVersion: 0));

            var persistedBatch = await repository.GetOwnedBatchAsync(
                batch.Id,
                owner,
                batch.ConnectionId);
            var persistedItem = await repository.GetOwnedItemAsync(
                batch.Id,
                item.ClientItemId,
                owner);
            Assert.Equal(McpImportBatchState.Prepared, persistedBatch!.State);
            Assert.Equal("operation-a", persistedItem!.OperationId);
            Assert.Empty(persistedItem.NormalizedDataCiphertext);
        }
        finally
        {
            await CleanupAsync(client, owner);
        }
    }

    private static async Task CleanupAsync(
        IMongoClient client,
        params string[] ownerIds)
    {
        var database = client.GetDatabase("FinanMap");
        await database.GetCollection<McpImportItem>("McpImportItems")
            .DeleteManyAsync(Builders<McpImportItem>.Filter.In(
                item => item.UserId,
                ownerIds));
        await database.GetCollection<McpImportBatch>("McpImportBatches")
            .DeleteManyAsync(Builders<McpImportBatch>.Filter.In(
                item => item.UserId,
                ownerIds));
    }
}
