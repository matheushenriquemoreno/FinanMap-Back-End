#nullable enable

using Domain.Mcp.Entities;

namespace Domain.Mcp.Repositories;

public sealed record McpImportBatchCreateResult(
    McpImportBatch Batch,
    bool Created);

public interface IMcpImportRepository
{
    Task<McpImportBatchCreateResult> CreateOrGetBatchAsync(
        McpImportBatch batch,
        CancellationToken cancellationToken = default);

    Task AddItemsAsync(
        IReadOnlyCollection<McpImportItem> items,
        CancellationToken cancellationToken = default);

    Task<McpImportBatch?> GetOwnedBatchAsync(
        string batchId,
        string userId,
        string connectionId,
        CancellationToken cancellationToken = default);

    Task<McpImportItem?> GetOwnedItemAsync(
        string batchId,
        string clientItemId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<McpImportItem>> ListOwnedItemsAsync(
        string batchId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<McpImportItem>> FindOwnedByFingerprintAsync(
        string userId,
        string fingerprint,
        int limit,
        CancellationToken cancellationToken = default);

    Task<bool> ReplaceBatchAsync(
        McpImportBatch batch,
        string userId,
        int expectedVersion,
        CancellationToken cancellationToken = default);

    Task<bool> ReplaceItemAsync(
        McpImportItem item,
        string userId,
        int expectedVersion,
        CancellationToken cancellationToken = default);
}
