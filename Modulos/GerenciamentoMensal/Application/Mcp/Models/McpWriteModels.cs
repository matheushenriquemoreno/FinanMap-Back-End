#nullable enable

using Domain.Enum;
using Domain.Enums;
using Domain.Mcp.Entities;
using Domain.Mcp.Enums;

namespace Application.Mcp.Models;

public enum McpWriteEntity
{
    Category,
    Income,
    Expense,
    Investment,
    FixedCost
}

public sealed record McpCategoryCreatePreviewInput(
    string RequestId,
    string Name,
    TipoCategoria Type);

public sealed record McpCategoryUpdatePreviewInput(
    string RequestId,
    string Id,
    string Name);

public sealed record McpCategoryDeletePreviewInput(
    string RequestId,
    string Id);

public sealed record McpIncomeCreatePreviewInput(
    string RequestId,
    int Year,
    int Month,
    string Description,
    string Amount,
    string CategoryId);

public sealed record McpIncomeUpdatePreviewInput(
    string RequestId,
    string Id,
    string? Description,
    string? Amount,
    string? CategoryId);

public sealed record McpIncomeDeletePreviewInput(
    string RequestId,
    string Id);

public sealed record McpExpenseCreatePreviewInput(
    string RequestId,
    int Year,
    int Month,
    string Description,
    string Amount,
    string CategoryId,
    bool IsInstallment,
    bool IsRecurring,
    int? RecurrenceCount,
    string? GroupingExpenseId);

public sealed record McpExpenseUpdatePreviewInput(
    string RequestId,
    string Id,
    string? Description,
    string? Amount,
    string? CategoryId,
    string? GroupingExpenseId,
    ModificadorLote? BatchModifier);

public sealed record McpExpenseDeletePreviewInput(
    string RequestId,
    string Id,
    ModificadorLote? BatchModifier);

public sealed record McpInvestmentCreatePreviewInput(
    string RequestId,
    int Year,
    int Month,
    string Description,
    string Amount,
    string CategoryId);

public sealed record McpInvestmentUpdatePreviewInput(
    string RequestId,
    string Id,
    string? Description,
    string? Amount,
    string? CategoryId);

public sealed record McpInvestmentDeletePreviewInput(
    string RequestId,
    string Id);

public sealed record McpFixedCostCreatePreviewInput(
    string RequestId,
    string Name,
    int DueDay,
    string? CategoryId);

public sealed record McpFixedCostUpdatePreviewInput(
    string RequestId,
    string Id,
    string? Name,
    int? DueDay,
    string? CategoryId,
    bool? Active);

public sealed record McpFixedCostDeletePreviewInput(
    string RequestId,
    string Id);

public sealed record McpOperationConfirmInput(
    string PreviewId,
    string PayloadHash,
    string Decision);

public sealed record McpOperationCancelInput(string PreviewId);

public sealed record McpOperationStatusInput(string OperationId);

public sealed record McpPreviewTarget(
    string EntityType,
    string EntityId);

public sealed record McpPreviewChange(
    string Field,
    string Label,
    object? CurrentValue,
    object? ProposedValue);

public sealed record McpPreviewData(
    string PreviewId,
    string Action,
    IReadOnlyList<McpPreviewTarget> Targets,
    IReadOnlyDictionary<string, object?>? CurrentValues,
    IReadOnlyDictionary<string, object?> ProposedValues,
    IReadOnlyList<McpPreviewChange> Changes,
    bool Irreversible,
    string RequiredDecision,
    DateTime ExpiresAtUtc,
    string PayloadHash);

public sealed record McpOperationData(
    string OperationId,
    string State,
    string? EntityType,
    string? EntityId,
    string Summary,
    bool RetryAllowed,
    IReadOnlyDictionary<string, object?>? Result);

public sealed record McpReconciliationBatchResult(
    int Scanned,
    int Completed,
    int Rejected,
    int Unknown,
    int Skipped);

public sealed record McpWriteCommand(
    McpWriteEntity Entity,
    McpPreviewAction Action,
    string? TargetId,
    IReadOnlyDictionary<string, object?> Values,
    IReadOnlyDictionary<string, object?>? ExpectedValues = null,
    IReadOnlyList<McpWriteStepPlan>? Steps = null,
    string? StepType = null);

public sealed record McpWriteStepPlan(
    string Name,
    string? TargetId,
    IReadOnlyDictionary<string, object?> Values,
    IReadOnlyDictionary<string, object?>? ExpectedValues,
    string? Type = null);

public sealed record McpDomainPreparation(
    McpWriteCommand Command,
    IReadOnlyDictionary<string, object?>? CurrentValues,
    IReadOnlyDictionary<string, object?> ProposedValues,
    IReadOnlyList<McpSnapshotHash> Snapshots,
    IReadOnlyList<McpToolError> Errors)
{
    public bool IsReady => Errors.Count == 0;

    public static McpDomainPreparation Ready(
        McpWriteCommand command,
        IReadOnlyDictionary<string, object?>? currentValues,
        IReadOnlyDictionary<string, object?> proposedValues,
        IReadOnlyList<McpSnapshotHash> snapshots) =>
        new(command, currentValues, proposedValues, snapshots, []);

    public static McpDomainPreparation Rejected(
        McpWriteCommand command,
        params McpToolError[] errors) =>
        new(command, null, new Dictionary<string, object?>(), [], errors);
}

public enum McpDomainEffectState
{
    Completed,
    ConflictChanged,
    Rejected,
    Unknown
}

public enum McpApplicationMutationState
{
    Completed,
    ConflictChanged,
    Rejected,
    Unknown
}

public sealed record McpApplicationMutationResult(
    McpApplicationMutationState State,
    string? EntityId,
    string? EffectMarker,
    string? ResultHash,
    string? ErrorCode,
    string? Message);

public sealed record McpDomainEffect(
    McpDomainEffectState State,
    string? EntityId,
    string? EffectMarker,
    IReadOnlyDictionary<string, object?> Result,
    string? ErrorCode,
    string? Message)
{
    public static McpDomainEffect Completed(
        string entityId,
        string effectMarker,
        IReadOnlyDictionary<string, object?> result) =>
        new(
            McpDomainEffectState.Completed,
            entityId,
            effectMarker,
            result,
            null,
            null);

    public static McpDomainEffect ConflictChanged() =>
        new(
            McpDomainEffectState.ConflictChanged,
            null,
            null,
            new Dictionary<string, object?>(),
            "CONFLICT_CHANGED",
            "O registro mudou desde a prévia; prepare uma nova prévia.");

    public static McpDomainEffect Rejected(string errorCode, string message) =>
        new(
            McpDomainEffectState.Rejected,
            null,
            null,
            new Dictionary<string, object?>(),
            errorCode,
            message);

    public static McpDomainEffect Unknown(string message) =>
        new(
            McpDomainEffectState.Unknown,
            null,
            null,
            new Dictionary<string, object?>(),
            "EFFECT_OUTCOME_UNKNOWN",
            message);
}
