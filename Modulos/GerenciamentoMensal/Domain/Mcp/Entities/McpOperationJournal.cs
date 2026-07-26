#nullable enable

using Domain.Entity;
using Domain.Mcp.Enums;

namespace Domain.Mcp.Entities;

public sealed record McpTargetRef(string EntityType, string EntityId);

public sealed class McpOperationStep
{
    public string Name { get; private set; } = string.Empty;
    public McpOperationStepState State { get; private set; }
    public string? EffectMarker { get; private set; }
    public IReadOnlyDictionary<string, object?>? ResultSummary { get; private set; }
    public string? ErrorCode { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? FinishedAtUtc { get; private set; }

    private McpOperationStep()
    {
    }

    public McpOperationStep(
        string name,
        McpOperationStepState state,
        string? effectMarker,
        IReadOnlyDictionary<string, object?>? resultSummary,
        string? errorCode)
    {
        Name = name;
        State = state;
        EffectMarker = effectMarker;
        ResultSummary = resultSummary;
        ErrorCode = errorCode;
    }

    internal void Start(DateTime nowUtc)
    {
        if (State == McpOperationStepState.Completed)
            return;
        State = McpOperationStepState.Executing;
        StartedAtUtc ??= nowUtc;
    }

    internal void Complete(
        string effectMarker,
        IReadOnlyDictionary<string, object?> resultSummary,
        DateTime nowUtc)
    {
        State = McpOperationStepState.Completed;
        EffectMarker = effectMarker;
        ResultSummary = resultSummary;
        ErrorCode = null;
        FinishedAtUtc = nowUtc;
    }

    internal void Fail(string errorCode, bool unknown, DateTime nowUtc)
    {
        State = unknown
            ? McpOperationStepState.Unknown
            : McpOperationStepState.Failed;
        ErrorCode = errorCode;
        FinishedAtUtc = nowUtc;
    }
}

public sealed class McpOperationJournal : EntityBase
{
    public string? PreviewId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string ConnectionId { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public string ToolName { get; private set; } = string.Empty;
    public McpOperationClass OperationClass { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public string RequestHash { get; private set; } = string.Empty;
    public McpOperationState State { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTime? LeaseExpiresAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public IReadOnlyList<McpOperationStep> Steps { get; private set; } = [];
    public IReadOnlyList<McpTargetRef> TargetRefs { get; private set; } = [];
    public IReadOnlyDictionary<string, object?> SanitizedParameters { get; private set; } =
        new Dictionary<string, object?>();
    public IReadOnlyDictionary<string, object?> ResultSummary { get; private set; } =
        new Dictionary<string, object?>();
    public IReadOnlyDictionary<string, object?> Origin { get; private set; } =
        new Dictionary<string, object?>();
    public IReadOnlyList<string> ErrorCodes { get; private set; } = [];
    public long? DurationMs { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? FinishedAtUtc { get; private set; }
    public DateTime? ReconciledAtUtc { get; private set; }
    public int Version { get; private set; }

    private McpOperationJournal()
    {
    }

    public static McpOperationJournal Start(
        string userId,
        string connectionId,
        string correlationId,
        string toolName,
        McpOperationClass operationClass,
        IReadOnlyDictionary<string, object?>? sanitizedParameters = null,
        IReadOnlyDictionary<string, object?>? origin = null,
        string? idempotencyKey = null,
        string requestHash = "",
        string? previewId = null,
        IReadOnlyList<McpTargetRef>? targetRefs = null,
        IReadOnlyList<McpOperationStep>? steps = null,
        DateTime? startedAtUtc = null)
    {
        return new McpOperationJournal
        {
            Id = Guid.NewGuid().ToString("N")[..24],
            PreviewId = previewId,
            UserId = userId,
            ConnectionId = connectionId,
            CorrelationId = correlationId,
            ToolName = toolName,
            OperationClass = operationClass,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash,
            State = McpOperationState.Received,
            Steps = steps?.ToArray() ?? [],
            TargetRefs = targetRefs?.ToArray() ?? [],
            SanitizedParameters = sanitizedParameters ?? new Dictionary<string, object?>(),
            Origin = origin ?? new Dictionary<string, object?>(),
            StartedAtUtc = startedAtUtc ?? DateTime.UtcNow
        };
    }

    public bool TryAcquireLease(string leaseOwner, DateTime nowUtc, TimeSpan leaseDuration)
    {
        if (State is McpOperationState.Completed or
            McpOperationState.PartiallyCompleted or
            McpOperationState.Failed or
            McpOperationState.Rejected or
            McpOperationState.Unknown)
        {
            return false;
        }

        if (LeaseExpiresAtUtc > nowUtc &&
            !string.Equals(LeaseOwner, leaseOwner, StringComparison.Ordinal))
        {
            return false;
        }

        LeaseOwner = leaseOwner;
        LeaseExpiresAtUtc = nowUtc.Add(leaseDuration);
        AttemptCount++;
        State = McpOperationState.Executing;
        NextAttemptAtUtc = LeaseExpiresAtUtc;
        Version++;
        return true;
    }

    public void StartStep(string name, string leaseOwner, DateTime nowUtc)
    {
        EnsureLease(leaseOwner, nowUtc);
        FindStep(name).Start(nowUtc);
        Version++;
    }

    public void CompleteStep(
        string name,
        string effectMarker,
        IReadOnlyDictionary<string, object?> resultSummary,
        DateTime nowUtc)
    {
        FindStep(name).Complete(effectMarker, resultSummary, nowUtc);
        Version++;
    }

    public void FailStep(string name, string errorCode, bool unknown, DateTime nowUtc)
    {
        FindStep(name).Fail(errorCode, unknown, nowUtc);
        Version++;
    }

    public void ScheduleReconciliation(DateTime nextAttemptAtUtc)
    {
        State = McpOperationState.Reconciling;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
        NextAttemptAtUtc = nextAttemptAtUtc;
        Version++;
    }

    public void SetResultSummary(
        IReadOnlyDictionary<string, object?> resultSummary)
    {
        ResultSummary = new Dictionary<string, object?>(resultSummary);
        Version++;
    }

    public void EnsureTargetRef(string entityType, string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityType) ||
            string.IsNullOrWhiteSpace(entityId) ||
            TargetRefs.Any(item =>
                string.Equals(item.EntityType, entityType, StringComparison.Ordinal) &&
                string.Equals(item.EntityId, entityId, StringComparison.Ordinal)))
        {
            return;
        }

        TargetRefs = [.. TargetRefs, new McpTargetRef(entityType, entityId)];
        Version++;
    }

    public void MarkUnknown(string errorCode, DateTime nowUtc)
    {
        State = McpOperationState.Unknown;
        ErrorCodes = [.. ErrorCodes, errorCode];
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
        NextAttemptAtUtc = null;
        FinishedAtUtc = nowUtc;
        DurationMs = Duration(nowUtc);
        Version++;
    }

    public void Reject(string errorCode, DateTime? nowUtc = null)
    {
        var finishedAt = nowUtc ?? DateTime.UtcNow;
        State = McpOperationState.Rejected;
        ErrorCodes = [.. ErrorCodes, errorCode];
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
        NextAttemptAtUtc = null;
        FinishedAtUtc = finishedAt;
        DurationMs = Duration(finishedAt);
        Version++;
    }

    public void Complete(IReadOnlyDictionary<string, object?>? resultSummary = null)
    {
        CompleteAt(resultSummary, DateTime.UtcNow);
    }

    public void CompleteAt(
        IReadOnlyDictionary<string, object?>? resultSummary,
        DateTime nowUtc,
        bool reconciled = false)
    {
        State = McpOperationState.Completed;
        ResultSummary = resultSummary ?? new Dictionary<string, object?>();
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
        NextAttemptAtUtc = null;
        FinishedAtUtc = nowUtc;
        if (reconciled)
            ReconciledAtUtc = nowUtc;
        DurationMs = Duration(nowUtc);
        Version++;
    }

    public void Fail(string errorCode)
    {
        FailAt(errorCode, DateTime.UtcNow);
    }

    public void FailAt(string errorCode, DateTime nowUtc)
    {
        State = McpOperationState.Failed;
        ErrorCodes = [.. ErrorCodes, errorCode];
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
        NextAttemptAtUtc = null;
        FinishedAtUtc = nowUtc;
        DurationMs = Duration(nowUtc);
        Version++;
    }

    private McpOperationStep FindStep(string name) =>
        Steps.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal))
        ?? throw new InvalidOperationException($"Passo MCP desconhecido: {name}.");

    private void EnsureLease(string leaseOwner, DateTime nowUtc)
    {
        if (!string.Equals(LeaseOwner, leaseOwner, StringComparison.Ordinal) ||
            LeaseExpiresAtUtc <= nowUtc)
        {
            throw new InvalidOperationException("Lease MCP ausente ou expirado.");
        }
    }

    private long Duration(DateTime nowUtc) =>
        Math.Max(0, (long)(nowUtc - StartedAtUtc).TotalMilliseconds);
}
