using Domain.Mcp.Entities;
using Domain.Mcp.Enums;
using Domain.Mcp.Repositories;
using Infra.Data.Mongo.RepositoryBase;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Mcp;

public sealed class McpOperationJournalRepository(IMongoClient mongoClient)
    : RepositoryMongoBase<McpOperationJournal>(mongoClient),
      IMcpOperationJournalRepository,
      IMcpAuditQueryRepository
{
    public override string GetCollectionName() => "McpOperationJournal";

    public Task AddAsync(McpOperationJournal journal, CancellationToken cancellationToken = default) =>
        _entityCollection.InsertOneAsync(journal, cancellationToken: cancellationToken);

    public Task CompleteAsync(
        McpOperationJournal journal, object? resultSummary, CancellationToken cancellationToken = default) =>
        _entityCollection.ReplaceOneAsync(
            item => item.Id == journal.Id, journal, cancellationToken: cancellationToken);

    public Task FailAsync(
        McpOperationJournal journal, string errorCode, CancellationToken cancellationToken = default) =>
        _entityCollection.ReplaceOneAsync(
            item => item.Id == journal.Id, journal, cancellationToken: cancellationToken);

    public Task<McpOperationJournal?> GetOwnedAsync(
        string id, string userId, CancellationToken cancellationToken = default) =>
        _entityCollection.Find(item => item.Id == id && item.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

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
