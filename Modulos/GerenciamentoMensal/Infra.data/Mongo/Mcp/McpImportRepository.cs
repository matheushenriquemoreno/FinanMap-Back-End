#nullable enable

using Domain.Mcp.Entities;
using Domain.Mcp.Repositories;
using Infra.Data.Mongo.Config;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Mcp;

public sealed class McpImportRepository : IMcpImportRepository
{
    private readonly IMongoCollection<McpImportBatch> _batches;
    private readonly IMongoCollection<McpImportItem> _items;

    public McpImportRepository(IMongoClient mongoClient)
    {
        var database = mongoClient.GetDatabase();
        _batches = database.GetCollection<McpImportBatch>("McpImportBatches");
        _items = database.GetCollection<McpImportItem>("McpImportItems");
    }

    public async Task<McpImportBatchCreateResult> CreateOrGetBatchAsync(
        McpImportBatch batch,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _batches.InsertOneAsync(
                batch,
                cancellationToken: cancellationToken);
            return new McpImportBatchCreateResult(batch, true);
        }
        catch (MongoWriteException exception)
            when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await _batches.Find(item =>
                    item.UserId == batch.UserId &&
                    item.ConnectionId == batch.ConnectionId &&
                    item.BatchKey == batch.BatchKey)
                .FirstAsync(cancellationToken);
            return new McpImportBatchCreateResult(existing, false);
        }
    }

    public Task AddItemsAsync(
        IReadOnlyCollection<McpImportItem> items,
        CancellationToken cancellationToken = default) =>
        items.Count == 0
            ? Task.CompletedTask
            : _items.InsertManyAsync(
                items,
                cancellationToken: cancellationToken);

    public async Task<McpImportBatch?> GetOwnedBatchAsync(
        string batchId,
        string userId,
        string connectionId,
        CancellationToken cancellationToken = default) =>
        await _batches.Find(item =>
                item.Id == batchId &&
                item.UserId == userId &&
                item.ConnectionId == connectionId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<McpImportItem?> GetOwnedItemAsync(
        string batchId,
        string clientItemId,
        string userId,
        CancellationToken cancellationToken = default) =>
        await _items.Find(item =>
                item.BatchId == batchId &&
                item.ClientItemId == clientItemId &&
                item.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<McpImportItem>> ListOwnedItemsAsync(
        string batchId,
        string userId,
        CancellationToken cancellationToken = default) =>
        await _items.Find(item =>
                item.BatchId == batchId &&
                item.UserId == userId)
            .SortBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.ClientItemId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<McpImportItem>> FindOwnedByFingerprintAsync(
        string userId,
        string fingerprint,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(limit));

        return await _items.Find(item =>
                item.UserId == userId &&
                item.Fingerprint == fingerprint)
            .SortByDescending(item => item.CreatedAtUtc)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ReplaceBatchAsync(
        McpImportBatch batch,
        string userId,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var result = await _batches.ReplaceOneAsync(
            item =>
                item.Id == batch.Id &&
                item.UserId == userId &&
                item.Version == expectedVersion,
            batch,
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    public async Task<bool> ReplaceItemAsync(
        McpImportItem item,
        string userId,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var result = await _items.ReplaceOneAsync(
            stored =>
                stored.Id == item.Id &&
                stored.UserId == userId &&
                stored.Version == expectedVersion,
            item,
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }
}
