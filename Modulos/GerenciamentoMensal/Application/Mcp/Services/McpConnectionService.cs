using Application.Mcp.Interfaces;
using Domain.Mcp.Entities;
using Domain.Mcp.Enums;
using Domain.Mcp.Repositories;

namespace Application.Mcp.Services;

public sealed class McpConnectionService : IMcpConnectionValidator
{
    private readonly IMcpConnectionRepository _connections;
    private readonly IMcpAuthorizationInteractionRepository _interactions;
    private readonly IMcpAuthorizationGrantStore _authorizationGrants;
    private readonly IMcpOperationJournalRepository _journals;

    public McpConnectionService(
        IMcpConnectionRepository connections,
        IMcpAuthorizationInteractionRepository interactions,
        IMcpAuthorizationGrantStore authorizationGrants,
        IMcpOperationJournalRepository journals)
    {
        _connections = connections;
        _interactions = interactions;
        _authorizationGrants = authorizationGrants;
        _journals = journals;
    }

    public async Task<McpConnection> ApproveAsync(
        string interactionId,
        string userId,
        IReadOnlyCollection<string> approvedScopes,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var journal = await StartJournalAsync(
            userId,
            interactionId,
            correlationId,
            "mcp_authorization_approve",
            McpOperationClass.Auth,
            cancellationToken);

        try
        {
            var interaction = await _interactions.GetAsync(interactionId, cancellationToken)
                ?? throw new McpAuthorizationInteractionNotFoundException();

            interaction.Approve(userId, approvedScopes, DateTime.UtcNow);
            var authorizationId = await _authorizationGrants.CreateAsync(
                userId, interaction, approvedScopes, cancellationToken);
            var connection = McpConnection.CreateActive(
                userId,
                authorizationId,
                interaction.ClientId,
                interaction.ClientName,
                approvedScopes);

            await _connections.AddAsync(connection, cancellationToken);
            interaction.BindConnection(connection.Id);
            await _interactions.UpdateAsync(interaction, cancellationToken);
            journal.Complete(new Dictionary<string, object?> { ["connectionId"] = connection.Id });
            await _journals.CompleteAsync(journal, journal.ResultSummary, cancellationToken);
            return connection;
        }
        catch
        {
            journal.Fail("AUTHORIZATION_APPROVAL_FAILED");
            await _journals.FailAsync(journal, "AUTHORIZATION_APPROVAL_FAILED", cancellationToken);
            throw;
        }
    }

    public Task<McpConnection?> GetAsync(
        string connectionId, string userId, CancellationToken cancellationToken = default) =>
        _connections.GetOwnedAsync(connectionId, userId, cancellationToken);

    public Task<IReadOnlyList<McpConnection>> ListAsync(
        string userId, CancellationToken cancellationToken = default) =>
        _connections.ListOwnedAsync(userId, cancellationToken);

    public async Task DenyAsync(
        string interactionId,
        string userId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var journal = await StartJournalAsync(
            userId,
            interactionId,
            correlationId,
            "mcp_authorization_deny",
            McpOperationClass.Auth,
            cancellationToken);

        try
        {
            var interaction = await _interactions.GetAsync(interactionId, cancellationToken)
                ?? throw new McpAuthorizationInteractionNotFoundException();
            interaction.Deny(userId, DateTime.UtcNow);
            await _interactions.UpdateAsync(interaction, cancellationToken);
            journal.Complete(new Dictionary<string, object?> { ["status"] = "denied" });
            await _journals.CompleteAsync(journal, journal.ResultSummary, cancellationToken);
        }
        catch
        {
            journal.Fail("AUTHORIZATION_DENIAL_FAILED");
            await _journals.FailAsync(journal, "AUTHORIZATION_DENIAL_FAILED", cancellationToken);
            throw;
        }
    }

    public async Task<McpConnection> RevokeAsync(
        string connectionId,
        string userId,
        string? reasonCode,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var journal = await StartJournalAsync(
            userId,
            connectionId,
            correlationId,
            "mcp_connection_revoke",
            McpOperationClass.Revoke,
            cancellationToken);

        try
        {
            var connection = await _connections.GetOwnedAsync(connectionId, userId, cancellationToken)
                ?? throw new McpConnectionNotFoundException();

            await _authorizationGrants.RevokeAsync(connection.AuthorizationId, cancellationToken);
            connection.Revoke(reasonCode, DateTime.UtcNow);
            await _connections.UpdateAsync(connection, cancellationToken);
            journal.Complete(new Dictionary<string, object?> { ["status"] = "revoked" });
            await _journals.CompleteAsync(journal, journal.ResultSummary, cancellationToken);
            return connection;
        }
        catch
        {
            journal.Fail("CONNECTION_REVOCATION_FAILED");
            await _journals.FailAsync(journal, "CONNECTION_REVOCATION_FAILED", cancellationToken);
            throw;
        }
    }

    public async Task<McpConnection> ValidateActiveAsync(
        string connectionId,
        string userId,
        string requiredScope,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connections.GetOwnedAsync(connectionId, userId, cancellationToken)
            ?? throw new McpConnectionNotFoundException();

        if (!connection.IsUsable(DateTime.UtcNow))
        {
            if (connection.Status == McpConnectionStatus.Active)
            {
                connection.Invalidate();
                await _connections.UpdateAsync(connection, cancellationToken);
            }
            throw new McpConnectionInactiveException();
        }

        if (!connection.HasScope(requiredScope))
            throw new McpScopeMissingException();

        if (!await _authorizationGrants.IsActiveAsync(connection.AuthorizationId, cancellationToken))
        {
            connection.Invalidate();
            await _connections.UpdateAsync(connection, cancellationToken);
            throw new McpConnectionInactiveException();
        }

        return connection;
    }

    private async Task<McpOperationJournal> StartJournalAsync(
        string userId,
        string connectionId,
        string correlationId,
        string toolName,
        McpOperationClass operationClass,
        CancellationToken cancellationToken)
    {
        var journal = McpOperationJournal.Start(
            userId,
            connectionId,
            correlationId,
            toolName,
            operationClass,
            origin: new Dictionary<string, object?>
            {
                ["channel"] = "finanmap-frontend"
            });
        try
        {
            await _journals.AddAsync(journal, cancellationToken);
            return journal;
        }
        catch (Exception exception)
        {
            throw new McpJournalUnavailableException(exception);
        }
    }
}

public sealed class McpJournalUnavailableException(Exception innerException)
    : Exception("O journal MCP não pôde ser persistido; a operação foi bloqueada.", innerException);

public sealed class McpConnectionNotFoundException()
    : Exception("Conexão MCP não encontrada.");

public sealed class McpConnectionInactiveException()
    : Exception("Conexão MCP inativa, expirada ou sem escopo.");

public sealed class McpScopeMissingException()
    : Exception("Conexão MCP sem o escopo necessário.");

public sealed class McpAuthorizationInteractionNotFoundException()
    : Exception("Interação de autorização MCP não encontrada.");
