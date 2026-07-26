using Domain.Entity;
using Domain.Mcp.Enums;

namespace Domain.Mcp.Entities;

public sealed class McpOperationJournal : EntityBase
{
    public string UserId { get; private set; } = string.Empty;
    public string ConnectionId { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public string ToolName { get; private set; } = string.Empty;
    public McpOperationClass OperationClass { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public string RequestHash { get; private set; } = string.Empty;
    public McpOperationState State { get; private set; }
    public IReadOnlyDictionary<string, object?> SanitizedParameters { get; private set; } =
        new Dictionary<string, object?>();
    public IReadOnlyDictionary<string, object?> ResultSummary { get; private set; } =
        new Dictionary<string, object?>();
    public IReadOnlyDictionary<string, object?> Origin { get; private set; } =
        new Dictionary<string, object?>();
    public IReadOnlyList<string> ErrorCodes { get; private set; } = [];
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? FinishedAtUtc { get; private set; }

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
        string requestHash = "")
    {
        return new McpOperationJournal
        {
            Id = Guid.NewGuid().ToString("N")[..24],
            UserId = userId,
            ConnectionId = connectionId,
            CorrelationId = correlationId,
            ToolName = toolName,
            OperationClass = operationClass,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash,
            State = McpOperationState.Received,
            SanitizedParameters = sanitizedParameters ?? new Dictionary<string, object?>(),
            Origin = origin ?? new Dictionary<string, object?>(),
            StartedAtUtc = DateTime.UtcNow
        };
    }

    public void Complete(IReadOnlyDictionary<string, object?>? resultSummary = null)
    {
        State = McpOperationState.Completed;
        ResultSummary = resultSummary ?? new Dictionary<string, object?>();
        FinishedAtUtc = DateTime.UtcNow;
    }

    public void Fail(string errorCode)
    {
        State = McpOperationState.Failed;
        ErrorCodes = [errorCode];
        FinishedAtUtc = DateTime.UtcNow;
    }
}
