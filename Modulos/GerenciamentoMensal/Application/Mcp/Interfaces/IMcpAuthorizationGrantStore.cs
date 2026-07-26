using Domain.Mcp.Entities;

namespace Application.Mcp.Interfaces;

public interface IMcpAuthorizationGrantStore
{
    Task<string> CreateAsync(
        string userId,
        McpAuthorizationInteraction interaction,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken = default);

    Task RevokeAsync(string authorizationId, CancellationToken cancellationToken = default);
    Task<bool> IsActiveAsync(string authorizationId, CancellationToken cancellationToken = default);
}

public interface IMcpConnectionValidator
{
    Task<McpConnection> ValidateActiveAsync(
        string connectionId,
        string userId,
        string requiredScope,
        CancellationToken cancellationToken = default);
}
