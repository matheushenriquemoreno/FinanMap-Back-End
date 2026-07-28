#nullable enable

using Domain.Enum;

namespace Application.Mcp.Models;

public enum McpImportEntityType
{
    Category,
    Income,
    Expense,
    Investment,
    FixedCost
}

public enum McpImportDuplicateDecision
{
    Skip,
    ImportAnyway
}

public enum McpImportConfirmationDecision
{
    IMPORT_VALID_ITEMS
}

public sealed record McpImportItemData(
    int? Year,
    int? Month,
    string? Description,
    string? Amount,
    TipoCategoria? CategoryType,
    string? Name,
    int? DueDay,
    string? CategoryId,
    bool? Active);

public sealed record McpImportItemInput(
    string ClientItemId,
    McpImportEntityType Type,
    string? SourceRef,
    McpImportItemData Data,
    string? CategoryHint,
    McpImportDuplicateDecision? DuplicateDecision);

public sealed record McpImportItemResult(
    string ClientItemId,
    string SourceRef,
    string Type,
    string ValidationState,
    string ExecutionState,
    string? EntityId,
    string? OperationId,
    IReadOnlyList<McpToolError> Errors,
    IReadOnlyList<string> Suggestions);

public sealed record McpImportCounts(
    int Total,
    int Valid,
    int Invalid,
    int Pending,
    int PossibleDuplicate,
    int Skipped,
    int Completed,
    int Failed,
    int Unknown);

public sealed record McpImportTotals(
    string Income,
    string Expense,
    string Investment,
    string FixedCost);

public sealed record McpImportPreviewData(
    string BatchId,
    string State,
    McpImportCounts Counts,
    McpImportTotals Totals,
    IReadOnlyList<McpImportItemResult> Items,
    string RequiredDecision,
    DateTime ExpiresAtUtc,
    string PayloadHash,
    IReadOnlyList<string> Guidance);

public sealed record McpImportStatusData(
    string BatchId,
    string State,
    string? ParentBatchId,
    McpImportCounts Counts,
    McpImportTotals Totals,
    IReadOnlyList<McpImportItemResult> Items,
    IReadOnlyList<string> Guidance);

public sealed record McpImportCategoryMatch(
    string Id,
    string Name,
    TipoCategoria Type);

public interface IMcpImportCategoryResolver
{
    Task<IReadOnlyList<McpImportCategoryMatch>> FindMatchesAsync(
        string userId,
        string hint,
        TipoCategoria expectedType,
        CancellationToken cancellationToken = default);
}

public interface IMcpImportService
{
    Task<McpToolEnvelope<McpImportPreviewData>> PrepareAsync(
        McpCallContext context,
        string requestId,
        IReadOnlyList<McpImportItemInput> items,
        CancellationToken cancellationToken);

    Task<McpToolEnvelope<McpImportStatusData>> ConfirmAsync(
        McpCallContext context,
        string batchId,
        string payloadHash,
        McpImportConfirmationDecision decision,
        CancellationToken cancellationToken);

    Task<McpToolEnvelope<McpImportStatusData>> GetStatusAsync(
        McpCallContext context,
        string batchId,
        CancellationToken cancellationToken);

    Task<McpToolEnvelope<McpImportPreviewData>> PrepareCorrectionAsync(
        McpCallContext context,
        string requestId,
        string parentBatchId,
        IReadOnlyList<McpImportItemInput> items,
        CancellationToken cancellationToken);
}
