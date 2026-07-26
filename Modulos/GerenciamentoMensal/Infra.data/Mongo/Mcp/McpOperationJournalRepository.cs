#nullable enable

using Domain.Mcp.Entities;
using Domain.Mcp.Enums;
using Domain.Mcp.Repositories;
using Infra.Data.Mongo.RepositoryBase;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Mcp;

public sealed class McpOperationJournalRepository(IMongoClient mongoClient)
    : RepositoryMongoBase<McpOperationJournal>(mongoClient),
      IMcpOperationJournalRepository,
      IMcpConfirmationJournalRepository,
      IMcpAuditQueryRepository
{
    public override string GetCollectionName() => "McpOperationJournal";

    public Task AddAsync(McpOperationJournal journal, CancellationToken cancellationToken = default) =>
        _entityCollection.InsertOneAsync(journal, cancellationToken: cancellationToken);

    public async Task<McpJournalCreateResult> CreateOrGetAsync(
        McpOperationJournal journal,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _entityCollection.InsertOneAsync(
                journal, cancellationToken: cancellationToken);
            return new McpJournalCreateResult(journal, true, false);
        }
        catch (MongoWriteException exception)
            when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            McpOperationJournal? existing = null;
            if (!string.IsNullOrWhiteSpace(journal.PreviewId))
            {
                existing = await _entityCollection.Find(item =>
                        item.PreviewId == journal.PreviewId)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            if (existing is null && !string.IsNullOrWhiteSpace(journal.IdempotencyKey))
            {
                existing = await _entityCollection.Find(item =>
                        item.UserId == journal.UserId &&
                        item.ConnectionId == journal.ConnectionId &&
                        item.ToolName == journal.ToolName &&
                        item.IdempotencyKey == journal.IdempotencyKey)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            if (existing is null)
                throw;
            return new McpJournalCreateResult(
                existing,
                false,
                !string.Equals(
                    existing.RequestHash,
                    journal.RequestHash,
                    StringComparison.Ordinal));
        }
    }

    public Task CompleteAsync(
        McpOperationJournal journal, object? resultSummary, CancellationToken cancellationToken = default) =>
        _entityCollection.ReplaceOneAsync(
            item => item.Id == journal.Id, journal, cancellationToken: cancellationToken);

    public Task FailAsync(
        McpOperationJournal journal, string errorCode, CancellationToken cancellationToken = default) =>
        _entityCollection.ReplaceOneAsync(
            item => item.Id == journal.Id, journal, cancellationToken: cancellationToken);

    public async Task<McpOperationJournal?> GetOwnedAsync(
        string id, string userId, CancellationToken cancellationToken = default) =>
        await _entityCollection.Find(item => item.Id == id && item.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<McpOperationJournal?> GetByPreviewAsync(
        string previewId,
        string userId,
        string connectionId,
        CancellationToken cancellationToken = default) =>
        await _entityCollection.Find(item =>
                item.PreviewId == previewId &&
                item.UserId == userId &&
                item.ConnectionId == connectionId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<McpOperationJournal?> TryAcquireLeaseAsync(
        string operationId,
        string leaseOwner,
        DateTime nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<McpOperationJournal>.Filter;
        var executable = builder.In(
            item => item.State,
            [
                McpOperationState.Received,
                McpOperationState.Executing,
                McpOperationState.Reconciling
            ]);
        var leaseAvailable = builder.Or(
            builder.Eq(item => item.LeaseOwner, leaseOwner),
            builder.Eq(item => item.LeaseOwner, null),
            builder.Lte(item => item.LeaseExpiresAtUtc, nowUtc));
        var filter = builder.Eq(item => item.Id, operationId) &
                     executable &
                     leaseAvailable;
        var leaseExpiry = nowUtc.Add(leaseDuration);
        var update = Builders<McpOperationJournal>.Update
            .Set(item => item.State, McpOperationState.Executing)
            .Set(item => item.LeaseOwner, leaseOwner)
            .Set(item => item.LeaseExpiresAtUtc, leaseExpiry)
            .Set(item => item.NextAttemptAtUtc, leaseExpiry)
            .Inc(item => item.AttemptCount, 1)
            .Inc(item => item.Version, 1);
        return await _entityCollection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<McpOperationJournal>
            {
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);
    }

    public async Task<bool> ReplaceAsync(
        McpOperationJournal journal,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var result = await _entityCollection.ReplaceOneAsync(
            item => item.Id == journal.Id && item.Version == expectedVersion,
            journal,
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    public async Task<IReadOnlyList<McpOperationJournal>> ListRecoverableAsync(
        DateTime nowUtc,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<McpOperationJournal>.Filter;
        var filter = builder.Eq(item => item.State, McpOperationState.Received) |
                     (builder.In(
                          item => item.State,
                          [
                              McpOperationState.Executing,
                              McpOperationState.Reconciling
                          ]) &
                      builder.Lte(item => item.NextAttemptAtUtc, nowUtc));
        return await _entityCollection.Find(filter)
            .SortBy(item => item.StartedAtUtc)
            .Limit(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken);
    }

    public async Task<McpAuditPage> ListOwnedAsync(
        string userId,
        DateTime? fromUtc,
        DateTime? toUtc,
        McpOperationClass? operationClass,
        McpOperationState? state,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<McpOperationJournal>.Filter;
        var filter = builder.Eq(item => item.UserId, userId);

        if (fromUtc.HasValue)
            filter &= builder.Gte(item => item.StartedAtUtc, fromUtc.Value);
        if (toUtc.HasValue)
            filter &= builder.Lte(item => item.StartedAtUtc, toUtc.Value);
        if (operationClass.HasValue)
            filter &= builder.Eq(item => item.OperationClass, operationClass.Value);
        if (state.HasValue)
            filter &= builder.Eq(item => item.State, state.Value);
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            var decoded = McpAuditCursor.Decode(cursor);
            filter &= builder.Or(
                builder.Lt(item => item.StartedAtUtc, decoded.StartedAtUtc),
                builder.And(
                    builder.Eq(item => item.StartedAtUtc, decoded.StartedAtUtc),
                    builder.Lt(item => item.Id, decoded.Id)));
        }

        var pageSize = Math.Clamp(limit, 1, 200);
        var items = await _entityCollection.Find(filter)
            .SortByDescending(item => item.StartedAtUtc)
            .ThenByDescending(item => item.Id)
            .Limit(pageSize + 1)
            .ToListAsync(cancellationToken);
        var hasNext = items.Count > pageSize;
        if (hasNext)
            items.RemoveAt(items.Count - 1);
        var nextCursor = hasNext && items.Count > 0
            ? McpAuditCursor.Encode(items[^1].StartedAtUtc, items[^1].Id)
            : null;

        return new McpAuditPage(items, nextCursor);
    }
}
