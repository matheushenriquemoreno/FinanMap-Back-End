#nullable enable

using Domain.Mcp.Entities;
using Domain.Mcp.Enums;

namespace Domain.Mcp.Repositories;

public sealed record McpPreviewCreateResult(
    McpPreview Preview,
    bool Created,
    bool PayloadConflict);

public interface IMcpPreviewRepository
{
    Task<McpPreviewCreateResult> CreateOrGetAsync(
        McpPreview preview,
        CancellationToken cancellationToken = default);

    Task<McpPreview?> GetOwnedAsync(
        string previewId,
        string userId,
        string connectionId,
        CancellationToken cancellationToken = default);

    Task<McpPreview?> GetByRequestAsync(
        string userId,
        string connectionId,
        string toolName,
        string requestId,
        CancellationToken cancellationToken = default);

    Task<McpPreview?> TryReserveAsync(
        string previewId,
        string userId,
        string connectionId,
        string payloadHash,
        McpRequiredDecision decision,
        string operationId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<McpPreview?> TryCancelAsync(
        string previewId,
        string userId,
        string connectionId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<bool> ReplaceAsync(
        McpPreview preview,
        int expectedVersion,
        CancellationToken cancellationToken = default);
}

public sealed record McpJournalCreateResult(
    McpOperationJournal Journal,
    bool Created,
    bool RequestConflict);

public interface IMcpConfirmationJournalRepository
{
    Task<McpJournalCreateResult> CreateOrGetAsync(
        McpOperationJournal journal,
        CancellationToken cancellationToken = default);

    Task<McpOperationJournal?> GetOwnedAsync(
        string operationId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<McpOperationJournal?> GetByPreviewAsync(
        string previewId,
        string userId,
        string connectionId,
        CancellationToken cancellationToken = default);

    Task<McpOperationJournal?> TryAcquireLeaseAsync(
        string operationId,
        string leaseOwner,
        DateTime nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task<bool> ReplaceAsync(
        McpOperationJournal journal,
        int expectedVersion,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<McpOperationJournal>> ListRecoverableAsync(
        DateTime nowUtc,
        int limit,
        CancellationToken cancellationToken = default);
}
