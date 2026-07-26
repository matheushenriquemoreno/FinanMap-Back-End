#nullable enable

using Domain.Mcp.Entities;
using Domain.Mcp.Enums;
using Domain.Mcp.Repositories;
using Infra.Data.Mongo.RepositoryBase;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Mcp;

public sealed class McpPreviewRepository(IMongoClient mongoClient)
    : RepositoryMongoBase<McpPreview>(mongoClient), IMcpPreviewRepository
{
    public override string GetCollectionName() => "McpPreviews";

    public async Task<McpPreviewCreateResult> CreateOrGetAsync(
        McpPreview preview,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _entityCollection.InsertOneAsync(
                preview, cancellationToken: cancellationToken);
            return new McpPreviewCreateResult(preview, true, false);
        }
        catch (MongoWriteException exception)
            when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await _entityCollection.Find(item =>
                    item.UserId == preview.UserId &&
                    item.ConnectionId == preview.ConnectionId &&
                    item.ToolName == preview.ToolName &&
                    item.RequestId == preview.RequestId)
                .FirstAsync(cancellationToken);
            return new McpPreviewCreateResult(
                existing,
                false,
                !string.Equals(
                    existing.PayloadHash,
                    preview.PayloadHash,
                    StringComparison.Ordinal));
        }
    }

    public async Task<McpPreview?> GetOwnedAsync(
        string previewId,
        string userId,
        string connectionId,
        CancellationToken cancellationToken = default) =>
        await _entityCollection.Find(item =>
                item.Id == previewId &&
                item.UserId == userId &&
                item.ConnectionId == connectionId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<McpPreview?> GetByRequestAsync(
        string userId,
        string connectionId,
        string toolName,
        string requestId,
        CancellationToken cancellationToken = default) =>
        await _entityCollection.Find(item =>
                item.UserId == userId &&
                item.ConnectionId == connectionId &&
                item.ToolName == toolName &&
                item.RequestId == requestId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<McpPreview?> TryReserveAsync(
        string previewId,
        string userId,
        string connectionId,
        string payloadHash,
        McpRequiredDecision decision,
        string operationId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<McpPreview>.Filter;
        var filter = builder.Eq(item => item.Id, previewId) &
                     builder.Eq(item => item.UserId, userId) &
                     builder.Eq(item => item.ConnectionId, connectionId) &
                     builder.Eq(item => item.PayloadHash, payloadHash) &
                     builder.Eq(item => item.RequiredDecision, decision) &
                     builder.Eq(item => item.State, McpPreviewState.Prepared) &
                     builder.Gt(item => item.ExpiresAtUtc, nowUtc);
        var update = Builders<McpPreview>.Update
            .Set(item => item.State, McpPreviewState.Executing)
            .Set(item => item.OperationId, operationId)
            .Set(item => item.ConsumedAtUtc, nowUtc)
            .Inc(item => item.Version, 1);
        return await _entityCollection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<McpPreview>
            {
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);
    }

    public async Task<McpPreview?> TryCancelAsync(
        string previewId,
        string userId,
        string connectionId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<McpPreview>.Filter;
        var filter = builder.Eq(item => item.Id, previewId) &
                     builder.Eq(item => item.UserId, userId) &
                     builder.Eq(item => item.ConnectionId, connectionId) &
                     builder.Eq(item => item.State, McpPreviewState.Prepared) &
                     builder.Gt(item => item.ExpiresAtUtc, nowUtc);
        var update = Builders<McpPreview>.Update
            .Set(item => item.State, McpPreviewState.Cancelled)
            .Set(item => item.ConsumedAtUtc, nowUtc)
            .Set(item => item.PayloadCiphertext, Array.Empty<byte>())
            .Inc(item => item.Version, 1);
        return await _entityCollection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<McpPreview>
            {
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);
    }

    public async Task<bool> ReplaceAsync(
        McpPreview preview,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var result = await _entityCollection.ReplaceOneAsync(
            item => item.Id == preview.Id && item.Version == expectedVersion,
            preview,
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }
}
