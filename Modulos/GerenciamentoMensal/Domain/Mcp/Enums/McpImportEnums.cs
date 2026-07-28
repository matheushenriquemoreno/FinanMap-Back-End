namespace Domain.Mcp.Enums;

public enum McpImportBatchState
{
    Preparing,
    Prepared,
    Processing,
    Partial,
    Completed,
    Failed
}

public enum McpImportItemType
{
    Category,
    Income,
    Expense,
    Investment,
    FixedCost
}

public enum McpImportValidationState
{
    Valid,
    Invalid,
    Pending,
    PossibleDuplicate,
    Skipped
}

public enum McpImportDuplicateDecision
{
    Skip,
    ImportAnyway
}

public enum McpImportExecutionState
{
    Pending,
    Completed,
    Failed,
    Unknown,
    AlreadyApplied,
    Skipped
}
