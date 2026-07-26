#nullable enable

using Domain.Entity;
using Domain.Mcp.Enums;

namespace Domain.Mcp.Entities;

public sealed record McpSnapshotHash(string EntityType, string EntityId, string Hash);

public sealed class McpPreview : EntityBase
{
    public string UserId { get; private set; } = string.Empty;
    public string ConnectionId { get; private set; } = string.Empty;
    public string ToolName { get; private set; } = string.Empty;
    public McpPreviewAction Action { get; private set; }
    public string RequestId { get; private set; } = string.Empty;
    public byte[] PayloadCiphertext { get; private set; } = [];
    public string PayloadHash { get; private set; } = string.Empty;
    public IReadOnlyList<McpSnapshotHash> SnapshotHashes { get; private set; } = [];
    public IReadOnlyDictionary<string, object?> SafeSummary { get; private set; } =
        new Dictionary<string, object?>();
    public McpRequiredDecision RequiredDecision { get; private set; }
    public McpPreviewState State { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }
    public string? OperationId { get; private set; }
    public DateTime PurgeAtUtc { get; private set; }
    public int Version { get; private set; }

    private McpPreview()
    {
    }

    public static McpPreview Prepare(
        string userId,
        string connectionId,
        string toolName,
        McpPreviewAction action,
        string requestId,
        byte[] payloadCiphertext,
        string payloadHash,
        IReadOnlyList<McpSnapshotHash> snapshotHashes,
        IReadOnlyDictionary<string, object?> safeSummary,
        McpRequiredDecision requiredDecision,
        DateTime nowUtc,
        TimeSpan validity,
        TimeSpan retention)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadHash);
        if (payloadCiphertext.Length == 0)
            throw new ArgumentException("Payload protegido obrigatório.", nameof(payloadCiphertext));

        return new McpPreview
        {
            Id = Guid.NewGuid().ToString("N")[..24],
            UserId = userId,
            ConnectionId = connectionId,
            ToolName = toolName,
            Action = action,
            RequestId = requestId,
            PayloadCiphertext = payloadCiphertext.ToArray(),
            PayloadHash = payloadHash,
            SnapshotHashes = snapshotHashes.ToArray(),
            SafeSummary = new Dictionary<string, object?>(safeSummary),
            RequiredDecision = requiredDecision,
            State = McpPreviewState.Prepared,
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.Add(validity),
            PurgeAtUtc = nowUtc.Add(retention)
        };
    }

    public bool TryReserve(
        string userId,
        string connectionId,
        string payloadHash,
        McpRequiredDecision decision,
        string operationId,
        DateTime nowUtc)
    {
        if (State != McpPreviewState.Prepared)
            return false;

        if (nowUtc >= ExpiresAtUtc)
        {
            State = McpPreviewState.Expired;
            Version++;
            return false;
        }

        if (!string.Equals(UserId, userId, StringComparison.Ordinal) ||
            !string.Equals(ConnectionId, connectionId, StringComparison.Ordinal) ||
            !string.Equals(PayloadHash, payloadHash, StringComparison.Ordinal) ||
            RequiredDecision != decision)
        {
            return false;
        }

        State = McpPreviewState.Executing;
        OperationId = operationId;
        ConsumedAtUtc = nowUtc;
        Version++;
        return true;
    }

    public bool TryCancel(string userId, string connectionId, DateTime nowUtc)
    {
        if (State != McpPreviewState.Prepared ||
            !string.Equals(UserId, userId, StringComparison.Ordinal) ||
            !string.Equals(ConnectionId, connectionId, StringComparison.Ordinal))
        {
            return false;
        }

        State = nowUtc >= ExpiresAtUtc
            ? McpPreviewState.Expired
            : McpPreviewState.Cancelled;
        ConsumedAtUtc = nowUtc;
        Version++;
        return true;
    }

    public void Complete()
    {
        State = McpPreviewState.Completed;
        PayloadCiphertext = [];
        Version++;
    }

    public void Fail(bool unknown)
    {
        State = unknown ? McpPreviewState.Unknown : McpPreviewState.Failed;
        if (!unknown)
            PayloadCiphertext = [];
        Version++;
    }
}
