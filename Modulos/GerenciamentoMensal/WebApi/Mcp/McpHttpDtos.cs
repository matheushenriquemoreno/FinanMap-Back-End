using Application.Mcp.Configuration;
using Domain.Mcp.Entities;
using Domain.Mcp.Enums;

namespace WebApi.Mcp;

public static class McpHttpDtoMapper
{
    public static McpConfigurationResponse MapConfiguration(McpFeatureOptions options)
    {
        var baseUrl = options.PublicBaseUrl.TrimEnd('/');
        return new McpConfigurationResponse(
            $"{baseUrl}/mcp",
            McpProtocolContract.Revision,
            McpProtocolContract.SdkVersion,
            new McpAuthorizationContract(
                McpProtocolContract.OAuthGrant,
                McpProtocolContract.PkceMethod,
                $"{baseUrl}/.well-known/oauth-protected-resource/mcp",
                $"{baseUrl}/.well-known/oauth-authorization-server"),
            [
                new McpProfileContract("read_only", McpProtocolContract.ReadOnlyScopes),
                new McpProfileContract("full_management", McpProtocolContract.FullManagementScopes)
            ],
            new McpFeatureContract(
                options.EndpointEnabled,
                options.WriteToolsEnabled,
                options.HistoryEnabled));
    }

    public static McpConnectionDto Map(McpConnection connection) =>
        new(
            connection.Id,
            connection.ClientId,
            connection.ClientName,
            connection.Scopes,
            ConnectionStatus(connection.Status),
            connection.CreatedAtUtc,
            connection.LastUsedAtUtc,
            connection.RevokedAtUtc,
            connection.RevocationReasonCode);

    public static McpAuditEventDto Map(McpOperationJournal journal) =>
        new(
            journal.Id,
            journal.CorrelationId,
            journal.ConnectionId,
            journal.ToolName,
            OperationClass(journal.OperationClass),
            OperationState(journal.State),
            journal.StartedAtUtc,
            journal.FinishedAtUtc,
            journal.Origin,
            journal.ResultSummary,
            journal.ErrorCodes);

    private static string ConnectionStatus(McpConnectionStatus status) => status switch
    {
        McpConnectionStatus.Pending => "pending",
        McpConnectionStatus.Active => "active",
        McpConnectionStatus.Invalid => "invalid",
        McpConnectionStatus.Revoked => "revoked",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private static string OperationClass(McpOperationClass operationClass) => operationClass switch
    {
        McpOperationClass.Read => "read",
        McpOperationClass.Preview => "preview",
        McpOperationClass.Confirm => "confirm",
        McpOperationClass.Import => "import",
        McpOperationClass.Auth => "auth",
        McpOperationClass.Revoke => "revoke",
        _ => throw new ArgumentOutOfRangeException(nameof(operationClass))
    };

    private static string OperationState(McpOperationState state) => state switch
    {
        McpOperationState.Received => "received",
        McpOperationState.Executing => "executing",
        McpOperationState.Reconciling => "reconciling",
        McpOperationState.Completed => "completed",
        McpOperationState.PartiallyCompleted => "partiallyCompleted",
        McpOperationState.Failed => "failed",
        McpOperationState.Rejected => "rejected",
        McpOperationState.Unknown => "unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };
}

public sealed record McpConfigurationResponse(
    string Endpoint,
    string ProtocolRevision,
    string SdkVersion,
    McpAuthorizationContract Authorization,
    IReadOnlyList<McpProfileContract> Profiles,
    McpFeatureContract Features);

public sealed record McpAuthorizationContract(
    string GrantType,
    string PkceMethod,
    string ProtectedResourceMetadataUrl,
    string AuthorizationServerMetadataUrl);

public sealed record McpProfileContract(string Id, IReadOnlyList<string> Scopes);

public sealed record McpFeatureContract(
    bool EndpointEnabled,
    bool WriteToolsEnabled,
    bool HistoryEnabled);

public sealed record McpConnectionDto(
    string Id,
    string ClientId,
    string ClientName,
    IReadOnlyList<string> Scopes,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? LastUsedAtUtc,
    DateTime? RevokedAtUtc,
    string? RevocationReasonCode);

public sealed record McpAuditEventDto(
    string Id,
    string CorrelationId,
    string ConnectionId,
    string ToolName,
    string OperationClass,
    string State,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    IReadOnlyDictionary<string, object?> Origin,
    IReadOnlyDictionary<string, object?> ResultSummary,
    IReadOnlyList<string> ErrorCodes);

public sealed record McpListResponse<T>(IReadOnlyList<T> Items, string? NextCursor = null);
public sealed record McpRevokeRequest(string? ReasonCode);
public sealed record McpApproveRequest(IReadOnlyList<string> Scopes);
public sealed record McpDenyRequest(string? ReasonCode);
