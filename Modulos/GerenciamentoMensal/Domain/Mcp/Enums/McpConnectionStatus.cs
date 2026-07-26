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
