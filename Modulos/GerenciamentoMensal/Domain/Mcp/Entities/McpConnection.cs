using Domain.Entity;
using Domain.Mcp.Enums;

namespace Domain.Mcp.Entities;

public sealed class McpConnection : EntityBase
{
    public string UserId { get; private set; } = string.Empty;
    public string AuthorizationId { get; private set; } = string.Empty;
    public string ClientId { get; private set; } = string.Empty;
    public string ClientName { get; private set; } = string.Empty;
    public IReadOnlyList<string> Scopes { get; private set; } = [];
    public McpConnectionStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public DateTime? LastUsedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public string? RevocationReasonCode { get; private set; }

    private McpConnection()
    {
    }

    public static McpConnection CreateActive(
        string userId,
        string authorizationId,
        string clientId,
        string clientName,
        IReadOnlyCollection<string> scopes,
        DateTime? expiresAtUtc = null,
        DateTime? createdAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        return new McpConnection
        {
            Id = Guid.NewGuid().ToString("N")[..24],
            UserId = userId,
            AuthorizationId = authorizationId,
            ClientId = clientId,
            ClientName = string.IsNullOrWhiteSpace(clientName) ? clientId : clientName.Trim(),
            Scopes = scopes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            Status = McpConnectionStatus.Active,
            CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    public bool IsUsable(DateTime utcNow) =>
        Status == McpConnectionStatus.Active &&
        (ExpiresAtUtc is null || ExpiresAtUtc > utcNow);

    public bool HasScope(string scope) =>
        Scopes.Contains(scope, StringComparer.Ordinal);

    public void Touch(DateTime utcNow)
    {
        if (!IsUsable(utcNow))
            throw new InvalidOperationException("A conexão MCP não está ativa.");

        LastUsedAtUtc = utcNow;
    }

    public void Invalidate()
    {
        if (Status != McpConnectionStatus.Revoked)
            Status = McpConnectionStatus.Invalid;
    }

    public void Revoke(string? reasonCode, DateTime utcNow)
    {
        Status = McpConnectionStatus.Revoked;
        RevokedAtUtc ??= utcNow;
        RevocationReasonCode = string.IsNullOrWhiteSpace(reasonCode) ? "user_request" : reasonCode.Trim();
    }
}
