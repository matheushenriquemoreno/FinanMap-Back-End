#nullable enable

using Domain.Entity;
using Domain.Mcp.Enums;

namespace Domain.Mcp.Entities;

public sealed class McpImportBatch : EntityBase
{
    public string UserId { get; private set; } = string.Empty;
    public string ConnectionId { get; private set; } = string.Empty;
    public string BatchKey { get; private set; } = string.Empty;
    public string? ParentBatchId { get; private set; }
    public McpImportBatchState State { get; private set; }
    public IReadOnlyDictionary<string, int> Counts { get; private set; } =
        new Dictionary<string, int>();
    public IReadOnlyDictionary<string, decimal> Totals { get; private set; } =
        new Dictionary<string, decimal>();
    public string PayloadHash { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime PurgeAtUtc { get; private set; }
    public DateTime? FinishedAtUtc { get; private set; }
    public string ConfirmationCorrelationId { get; private set; } = string.Empty;
    public string ConfirmationProtocolRevision { get; private set; } = string.Empty;
    public string ConfirmationClientId { get; private set; } = string.Empty;
    public string? ProcessingLeaseOwner { get; private set; }
    public DateTime? ProcessingLeaseUntilUtc { get; private set; }
    public int Version { get; private set; }

    private McpImportBatch()
    {
    }

    public static McpImportBatch Create(
        string userId,
        string connectionId,
        string batchKey,
        string? parentBatchId,
        DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(batchKey);

        return new McpImportBatch
        {
            Id = Guid.NewGuid().ToString("N")[..24],
            UserId = userId,
            ConnectionId = connectionId,
            BatchKey = batchKey,
            ParentBatchId = string.IsNullOrWhiteSpace(parentBatchId)
                ? null
                : parentBatchId,
            State = McpImportBatchState.Preparing,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = createdAtUtc.AddHours(24),
            PurgeAtUtc = createdAtUtc.AddHours(24)
        };
    }

    public void MarkPrepared(
        IReadOnlyDictionary<string, int> counts,
        IReadOnlyDictionary<string, decimal> totals,
        string payloadHash = "")
    {
        if (State != McpImportBatchState.Preparing)
            throw new InvalidOperationException("O lote não está em preparação.");

        ArgumentNullException.ThrowIfNull(counts);
        ArgumentNullException.ThrowIfNull(totals);
        Counts = new Dictionary<string, int>(counts);
        Totals = new Dictionary<string, decimal>(totals);
        PayloadHash = payloadHash;
        State = McpImportBatchState.Prepared;
        Version++;
    }

    public void StartProcessing(
        string correlationId = "",
        string protocolRevision = "",
        string clientId = "")
    {
        if (State != McpImportBatchState.Prepared)
            throw new InvalidOperationException("O lote não está preparado.");

        State = McpImportBatchState.Processing;
        ConfirmationCorrelationId = correlationId;
        ConfirmationProtocolRevision = protocolRevision;
        ConfirmationClientId = clientId;
        Version++;
    }

    public bool TryAcquireProcessingLease(
        string leaseOwner,
        DateTime nowUtc,
        TimeSpan leaseDuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);
        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        if (State != McpImportBatchState.Processing ||
            (ProcessingLeaseUntilUtc > nowUtc &&
             !string.Equals(
                 ProcessingLeaseOwner,
                 leaseOwner,
                 StringComparison.Ordinal)))
        {
            return false;
        }

        ProcessingLeaseOwner = leaseOwner;
        ProcessingLeaseUntilUtc = nowUtc.Add(leaseDuration);
        Version++;
        return true;
    }

    public void Finish(
        McpImportBatchState finalState,
        IReadOnlyDictionary<string, int> counts,
        IReadOnlyDictionary<string, decimal> totals,
        DateTime finishedAtUtc)
    {
        if (State != McpImportBatchState.Processing)
            throw new InvalidOperationException("O lote não está em processamento.");
        if (finalState is not (
            McpImportBatchState.Partial or
            McpImportBatchState.Completed or
            McpImportBatchState.Failed))
        {
            throw new ArgumentOutOfRangeException(
                nameof(finalState),
                "O estado final do lote é inválido.");
        }

        ArgumentNullException.ThrowIfNull(counts);
        ArgumentNullException.ThrowIfNull(totals);
        Counts = new Dictionary<string, int>(counts);
        Totals = new Dictionary<string, decimal>(totals);
        State = finalState;
        FinishedAtUtc = finishedAtUtc;
        ProcessingLeaseOwner = null;
        ProcessingLeaseUntilUtc = null;
        Version++;
    }

    public void Reconcile(
        McpImportBatchState finalState,
        IReadOnlyDictionary<string, int> counts,
        DateTime reconciledAtUtc)
    {
        if (State is not (
            McpImportBatchState.Partial or
            McpImportBatchState.Failed))
        {
            throw new InvalidOperationException("O lote não exige reconciliação.");
        }
        if (finalState is not (
            McpImportBatchState.Partial or
            McpImportBatchState.Completed or
            McpImportBatchState.Failed))
        {
            throw new ArgumentOutOfRangeException(nameof(finalState));
        }

        Counts = new Dictionary<string, int>(counts);
        State = finalState;
        FinishedAtUtc = reconciledAtUtc;
        Version++;
    }
}
