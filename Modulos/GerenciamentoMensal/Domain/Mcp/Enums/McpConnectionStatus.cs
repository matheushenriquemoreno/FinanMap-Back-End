namespace Domain.Mcp.Enums;

public enum McpConnectionStatus
{
    Pending,
    Active,
    Invalid,
    Revoked
}

public enum McpAuthorizationInteractionStatus
{
    Pending,
    Approved,
    Denied,
    Expired
}

public enum McpOperationClass
{
    Read,
    Preview,
    Confirm,
    Import,
    Auth,
    Revoke
}

public enum McpOperationState
{
    Received,
    Executing,
    Reconciling,
    Completed,
    PartiallyCompleted,
    Failed,
    Rejected,
    Unknown
}

public enum McpPreviewAction
{
    Create,
    Update,
    Delete,
    Import
}

public enum McpRequiredDecision
{
    ApplyChanges,
    DeletePermanently,
    ImportValidItems
}

public enum McpPreviewState
{
    Prepared,
    Executing,
    Completed,
    PartiallyCompleted,
    Failed,
    Cancelled,
    Expired,
    Unknown
}

public enum McpOperationStepState
{
    Pending,
    Executing,
    Completed,
    Failed,
    Unknown
}
