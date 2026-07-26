using Domain.Mcp.Entities;

namespace Domain.Mcp.Repositories;

public interface IMcpConnectionRepository
{
    Task AddAsync(McpConnection connection, CancellationToken cancellationToken = default);
    Task<McpConnection?> GetOwnedAsync(string id, string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<McpConnection>> ListOwnedAsync(string userId, CancellationToken cancellationToken = default);
    Task UpdateAsync(McpConnection connection, CancellationToken cancellationToken = default);
}

public interface IMcpAuthorizationInteractionRepository
{
    Task AddAsync(McpAuthorizationInteraction interaction, CancellationToken cancellationToken = default);
    Task<McpAuthorizationInteraction?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task UpdateAsync(McpAuthorizationInteraction interaction, CancellationToken cancellationToken = default);
}

public interface IMcpOperationJournalRepository
{
    Task AddAsync(McpOperationJournal journal, CancellationToken cancellationToken = default);
    Task CompleteAsync(
        McpOperationJournal journal, object? resultSummary, CancellationToken cancellationToken = default);
    Task FailAsync(
        McpOperationJournal journal, string errorCode, CancellationToken cancellationToken = default);
}

public interface IMcpAuditQueryRepository
{
    Task<McpOperationJournal?> GetOwnedAsync(
        string id, string userId, CancellationToken cancellationToken = default);

    Task<McpAuditPage> ListOwnedAsync(
        string userId,
        DateTime? fromUtc,
        DateTime? toUtc,
        Mcp.Enums.McpOperationClass? operationClass,
        Mcp.Enums.McpOperationState? state,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed record McpAuditPage(
    IReadOnlyList<McpOperationJournal> Items,
    string? NextCursor);
