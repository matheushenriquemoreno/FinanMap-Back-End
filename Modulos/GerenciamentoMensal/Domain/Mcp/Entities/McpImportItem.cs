#nullable enable

using Domain.Entity;
using Domain.Mcp.Enums;

namespace Domain.Mcp.Entities;

public sealed record McpImportSourceRef(
    string? Sheet,
    int? Row,
    string? Item);

public sealed record McpImportItemError(
    string Code,
    string Message,
    string? Field,
    string? Guidance);

public sealed class McpImportItem : EntityBase
{
    public string BatchId { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public string ClientItemId { get; private set; } = string.Empty;
    public McpImportSourceRef? SourceRef { get; private set; }
    public McpImportItemType Type { get; private set; }
    public byte[] NormalizedDataCiphertext { get; private set; } = [];
    public string Fingerprint { get; private set; } = string.Empty;
    public McpImportValidationState ValidationState { get; private set; }
    public McpImportDuplicateDecision? DuplicateDecision { get; private set; }
    public McpImportExecutionState ExecutionState { get; private set; }
    public IReadOnlyList<McpImportItemError> ErrorDetails { get; private set; } = [];
    public string? OperationId { get; private set; }
    public string? CreatedEntityId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? FinishedAtUtc { get; private set; }
    public DateTime PurgeAtUtc { get; private set; }
    public int Version { get; private set; }

    private McpImportItem()
    {
    }

    public static McpImportItem Create(
        string batchId,
        string userId,
        string clientItemId,
        McpImportSourceRef? sourceRef,
        McpImportItemType type,
        byte[] normalizedDataCiphertext,
        string fingerprint,
        McpImportValidationState validationState,
        IReadOnlyList<McpImportItemError> errorDetails,
        DateTime purgeAtUtc,
        DateTime? createdAtUtc = null,
        McpImportDuplicateDecision? duplicateDecision = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(batchId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientItemId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        ArgumentNullException.ThrowIfNull(normalizedDataCiphertext);
        ArgumentNullException.ThrowIfNull(errorDetails);

        return new McpImportItem
        {
            Id = Guid.NewGuid().ToString("N")[..24],
            BatchId = batchId,
            UserId = userId,
            ClientItemId = clientItemId,
            SourceRef = sourceRef,
            Type = type,
            NormalizedDataCiphertext = normalizedDataCiphertext.ToArray(),
            Fingerprint = fingerprint,
            ValidationState = validationState,
            DuplicateDecision = duplicateDecision,
            ExecutionState = validationState == McpImportValidationState.Skipped
                ? McpImportExecutionState.Skipped
                : McpImportExecutionState.Pending,
            ErrorDetails = errorDetails.ToArray(),
            CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow,
            PurgeAtUtc = purgeAtUtc
        };
    }

    public void RecordResult(
        string operationId,
        string? createdEntityId,
        DateTime finishedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);

        OperationId = operationId;
        CreatedEntityId = string.IsNullOrWhiteSpace(createdEntityId)
            ? null
            : createdEntityId;
        FinishedAtUtc = finishedAtUtc;
        NormalizedDataCiphertext = [];
        ExecutionState = McpImportExecutionState.Completed;
        Version++;
    }

    public void UpdateValidation(
        byte[] normalizedDataCiphertext,
        string fingerprint,
        McpImportValidationState validationState,
        IReadOnlyList<McpImportItemError> errorDetails)
    {
        if (ExecutionState != McpImportExecutionState.Pending)
            throw new InvalidOperationException("O item já possui resultado de execução.");
        ArgumentNullException.ThrowIfNull(normalizedDataCiphertext);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        ArgumentNullException.ThrowIfNull(errorDetails);

        NormalizedDataCiphertext = normalizedDataCiphertext.ToArray();
        Fingerprint = fingerprint;
        ValidationState = validationState;
        ErrorDetails = errorDetails.ToArray();
        if (validationState == McpImportValidationState.Skipped)
            ExecutionState = McpImportExecutionState.Skipped;
        Version++;
    }

    public void RecordFailure(
        string operationId,
        string errorCode,
        DateTime finishedAtUtc,
        bool unknown = false,
        byte[]? protectedPayload = null,
        string? message = null,
        string? guidance = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

        OperationId = operationId;
        ErrorDetails =
        [
            .. ErrorDetails,
            new McpImportItemError(
                errorCode,
                string.IsNullOrWhiteSpace(message)
                    ? "O item não foi concluído."
                    : message,
                null,
                string.IsNullOrWhiteSpace(guidance) ? null : guidance)
        ];
        FinishedAtUtc = finishedAtUtc;
        if (unknown && protectedPayload is not null)
            NormalizedDataCiphertext = protectedPayload.ToArray();
        ExecutionState = unknown
            ? McpImportExecutionState.Unknown
            : McpImportExecutionState.Failed;
        Version++;
    }

    public void MarkAlreadyApplied(string operationId, DateTime finishedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        OperationId = operationId;
        FinishedAtUtc = finishedAtUtc;
        ExecutionState = McpImportExecutionState.AlreadyApplied;
        NormalizedDataCiphertext = [];
        Version++;
    }
}
