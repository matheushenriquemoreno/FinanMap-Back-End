using Domain.Entity;
using Domain.Mcp.Enums;

namespace Domain.Mcp.Entities;

public sealed class McpAuthorizationInteraction : EntityBase
{
    public string ClientId { get; private set; } = string.Empty;
    public string ClientName { get; private set; } = string.Empty;
    public IReadOnlyList<string> RequestedScopes { get; private set; } = [];
    public DateTime ExpiresAtUtc { get; private set; }
    public McpAuthorizationInteractionStatus Status { get; private set; }
    public string? UserId { get; private set; }
    public IReadOnlyList<string> ApprovedScopes { get; private set; } = [];
    public string? ConnectionId { get; private set; }
    public string RedirectUri { get; private set; } = string.Empty;
    public string State { get; private set; } = string.Empty;
    public string CodeChallenge { get; private set; } = string.Empty;
    public string CodeChallengeMethod { get; private set; } = "S256";
    public string Resource { get; private set; } = string.Empty;

    private McpAuthorizationInteraction()
    {
    }

    public McpAuthorizationInteraction(
        string id,
        string clientId,
        string clientName,
        IReadOnlyCollection<string> requestedScopes,
        DateTime expiresAtUtc,
        string redirectUri = "",
        string state = "",
        string codeChallenge = "",
        string codeChallengeMethod = "S256",
        string resource = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        Id = id;
        ClientId = clientId;
        ClientName = string.IsNullOrWhiteSpace(clientName) ? clientId : clientName.Trim();
        RequestedScopes = requestedScopes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        ExpiresAtUtc = expiresAtUtc;
        RedirectUri = redirectUri;
        State = state;
        CodeChallenge = codeChallenge;
        CodeChallengeMethod = codeChallengeMethod;
        Resource = resource;
        Status = McpAuthorizationInteractionStatus.Pending;
    }

    public void Approve(string userId, IReadOnlyCollection<string> approvedScopes, DateTime utcNow)
    {
        Claim(userId, utcNow);
        EnsurePending(utcNow);

        if (approvedScopes.Count == 0 ||
            approvedScopes.Any(scope => !RequestedScopes.Contains(scope, StringComparer.Ordinal)))
            throw new InvalidOperationException("Os escopos aprovados devem ser um subconjunto não vazio dos solicitados.");

        ApprovedScopes = approvedScopes
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Status = McpAuthorizationInteractionStatus.Approved;
    }

    public void BindConnection(string connectionId)
    {
        if (Status != McpAuthorizationInteractionStatus.Approved)
            throw new InvalidOperationException("A interação precisa estar aprovada antes de vincular a conexão.");
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ConnectionId = connectionId;
    }

    public bool MatchesResumeRequest(
        string clientId,
        string redirectUri,
        string state,
        string codeChallenge,
        string codeChallengeMethod,
        string resource,
        IReadOnlyCollection<string> scopes)
    {
        return Status == McpAuthorizationInteractionStatus.Approved &&
               string.Equals(ClientId, clientId, StringComparison.Ordinal) &&
               string.Equals(RedirectUri, redirectUri, StringComparison.Ordinal) &&
               string.Equals(State, state, StringComparison.Ordinal) &&
               string.Equals(CodeChallenge, codeChallenge, StringComparison.Ordinal) &&
               string.Equals(CodeChallengeMethod, codeChallengeMethod, StringComparison.Ordinal) &&
               string.Equals(Resource, resource, StringComparison.Ordinal) &&
               ApprovedScopes.ToHashSet(StringComparer.Ordinal)
                   .SetEquals(scopes);
    }

    public void Deny(string userId, DateTime utcNow)
    {
        Claim(userId, utcNow);
        EnsurePending(utcNow);
        Status = McpAuthorizationInteractionStatus.Denied;
    }

    public void Claim(string userId, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        if (ExpiresAtUtc <= utcNow)
        {
            Status = McpAuthorizationInteractionStatus.Expired;
            throw new InvalidOperationException("A interação de autorização expirou.");
        }

        if (UserId is null)
        {
            if (Status != McpAuthorizationInteractionStatus.Pending)
                throw new InvalidOperationException("A interação de autorização já foi concluída.");
            UserId = userId;
            return;
        }

        if (!string.Equals(UserId, userId, StringComparison.Ordinal))
            throw new InvalidOperationException("A interação pertence a outro usuário.");
    }

    private void EnsurePending(DateTime utcNow)
    {
        if (ExpiresAtUtc <= utcNow)
        {
            Status = McpAuthorizationInteractionStatus.Expired;
            throw new InvalidOperationException("A interação de autorização expirou.");
        }

        if (Status != McpAuthorizationInteractionStatus.Pending)
            throw new InvalidOperationException("A interação de autorização já foi concluída.");
    }
}
