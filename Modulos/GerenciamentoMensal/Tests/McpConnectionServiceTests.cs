using Application.Mcp.Interfaces;
using Application.Mcp.Services;
using Domain.Mcp.Entities;
using Domain.Mcp.Enums;
using Domain.Mcp.Repositories;
using Xunit;

namespace Tests;

public class McpConnectionServiceTests
{
    [Fact]
    public async Task Approving_pending_consent_creates_authorization_then_active_connection()
    {
        var events = new List<string>();
        var interactions = new InteractionRepositoryFake(events, new McpAuthorizationInteraction(
            "interaction-1", "client-1", "Cliente de teste", ["mcp:read", "mcp:audit"],
            DateTime.UtcNow.AddMinutes(5)));
        var connections = new ConnectionRepositoryFake(events);
        var grants = new AuthorizationGrantStoreFake(events);
        var journals = new JournalRepositoryFake(events);
        var service = new McpConnectionService(connections, interactions, grants, journals);

        var connection = await service.ApproveAsync(
            "interaction-1", "owner-a", ["mcp:read", "mcp:audit"], "correlation-1");

        Assert.Equal(McpConnectionStatus.Active, connection.Status);
        Assert.Equal("owner-a", connection.UserId);
        Assert.Equal(["mcp:audit", "mcp:read"], interactions.Current!.ApprovedScopes);
        Assert.Equal(connection.Id, interactions.Current.ConnectionId);
        Assert.Equal(["journal", "authorization", "connection", "interaction", "journal-complete"], events);
    }

    [Fact]
    public async Task Revocation_invalidates_authorization_before_updating_product_view()
    {
        var events = new List<string>();
        var connection = McpConnection.CreateActive(
            "owner-a", "authorization-1", "client-1", "Cliente", ["mcp:read"]);
        var connections = new ConnectionRepositoryFake(events, connection);
        var grants = new AuthorizationGrantStoreFake(events);
        var service = new McpConnectionService(
            connections, new InteractionRepositoryFake(), grants, new JournalRepositoryFake(events));

        var revoked = await service.RevokeAsync(connection.Id, "owner-a", "user_request", "correlation-2");

        Assert.Equal(McpConnectionStatus.Revoked, revoked.Status);
        Assert.Equal(["journal", "authorization-revoked", "connection-updated", "journal-complete"], events);
        await Assert.ThrowsAsync<McpConnectionInactiveException>(() =>
            service.ValidateActiveAsync(connection.Id, "owner-a", "mcp:read"));
    }

    [Fact]
    public async Task Journal_failure_blocks_consent_and_any_authorization_effect()
    {
        var events = new List<string>();
        var interaction = new McpAuthorizationInteraction(
            "interaction-1", "client-1", "Cliente", ["mcp:read"], DateTime.UtcNow.AddMinutes(5));
        var service = new McpConnectionService(
            new ConnectionRepositoryFake(events),
            new InteractionRepositoryFake(events, interaction),
            new AuthorizationGrantStoreFake(events),
            new JournalRepositoryFake(events, failOnCreate: true));

        await Assert.ThrowsAsync<McpJournalUnavailableException>(() =>
            service.ApproveAsync("interaction-1", "owner-a", ["mcp:read"], "correlation-3"));

        Assert.Equal(["journal"], events);
    }

    [Fact]
    public async Task Another_owner_cannot_read_or_revoke_connection()
    {
        var connection = McpConnection.CreateActive(
            "owner-a", "authorization-1", "client-1", "Cliente", ["mcp:read"]);
        var service = new McpConnectionService(
            new ConnectionRepositoryFake([], connection),
            new InteractionRepositoryFake(),
            new AuthorizationGrantStoreFake([]),
            new JournalRepositoryFake([]));

        Assert.Null(await service.GetAsync(connection.Id, "owner-b"));
        await Assert.ThrowsAsync<McpConnectionNotFoundException>(() =>
            service.RevokeAsync(connection.Id, "owner-b", null, "correlation-4"));
    }

    [Fact]
    public void Expired_or_revoked_connection_is_not_usable()
    {
        var now = DateTime.UtcNow;
        var expired = McpConnection.CreateActive(
            "owner-a", "authorization-1", "client-1", "Cliente", ["mcp:read"], now.AddMinutes(-1));
        var revoked = McpConnection.CreateActive(
            "owner-a", "authorization-2", "client-2", "Cliente", ["mcp:read"]);
        revoked.Revoke("user_request", now);

        Assert.False(expired.IsUsable(now));
        Assert.False(revoked.IsUsable(now));
    }

    [Fact]
    public void Approved_interaction_only_resumes_the_exact_oauth_request()
    {
        var interaction = new McpAuthorizationInteraction(
            "interaction-a",
            "client-a",
            "Cliente",
            ["mcp:read", "mcp:audit"],
            DateTime.UtcNow.AddMinutes(5),
            "https://agent.example/callback",
            "state-a",
            "challenge-a",
            "S256",
            "https://api.example/mcp");
        interaction.Approve("owner-a", ["mcp:read"], DateTime.UtcNow);

        Assert.True(interaction.MatchesResumeRequest(
            "client-a",
            "https://agent.example/callback",
            "state-a",
            "challenge-a",
            "S256",
            "https://api.example/mcp",
            ["mcp:read"]));
        Assert.False(interaction.MatchesResumeRequest(
            "client-a",
            "https://agent.example/callback",
            "state-a",
            "different-challenge",
            "S256",
            "https://api.example/mcp",
            ["mcp:read"]));
    }

    private sealed class ConnectionRepositoryFake(List<string> events, params McpConnection[] seed)
        : IMcpConnectionRepository
    {
        private readonly List<McpConnection> _connections = [.. seed];

        public Task AddAsync(McpConnection connection, CancellationToken cancellationToken = default)
        {
            events.Add("connection");
            _connections.Add(connection);
            return Task.CompletedTask;
        }

        public Task<McpConnection?> GetOwnedAsync(
            string id, string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_connections.SingleOrDefault(x => x.Id == id && x.UserId == userId));

        public Task<IReadOnlyList<McpConnection>> ListOwnedAsync(
            string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<McpConnection>>(_connections.Where(x => x.UserId == userId).ToList());

        public Task UpdateAsync(McpConnection connection, CancellationToken cancellationToken = default)
        {
            events.Add("connection-updated");
            return Task.CompletedTask;
        }
    }

    private sealed class InteractionRepositoryFake : IMcpAuthorizationInteractionRepository
    {
        private readonly List<string> _events;
        private readonly List<McpAuthorizationInteraction> _interactions;
        public McpAuthorizationInteraction? Current => _interactions.SingleOrDefault();

        public InteractionRepositoryFake(List<string>? events = null, params McpAuthorizationInteraction[] seed)
        {
            _events = events ?? [];
            _interactions = [.. seed];
        }

        public Task<McpAuthorizationInteraction?> GetAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_interactions.SingleOrDefault(x => x.Id == id));

        public Task AddAsync(
            McpAuthorizationInteraction interaction, CancellationToken cancellationToken = default)
        {
            _interactions.Add(interaction);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(
            McpAuthorizationInteraction interaction, CancellationToken cancellationToken = default)
        {
            _events.Add("interaction");
            return Task.CompletedTask;
        }
    }

    private sealed class AuthorizationGrantStoreFake(List<string> events) : IMcpAuthorizationGrantStore
    {
        public Task<string> CreateAsync(
            string userId, McpAuthorizationInteraction interaction, IReadOnlyCollection<string> scopes,
            CancellationToken cancellationToken = default)
        {
            events.Add("authorization");
            return Task.FromResult("authorization-1");
        }

        public Task RevokeAsync(string authorizationId, CancellationToken cancellationToken = default)
        {
            events.Add("authorization-revoked");
            return Task.CompletedTask;
        }

        public Task<bool> IsActiveAsync(string authorizationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class JournalRepositoryFake(List<string> events, bool failOnCreate = false)
        : IMcpOperationJournalRepository
    {
        public Task AddAsync(McpOperationJournal journal, CancellationToken cancellationToken = default)
        {
            events.Add("journal");
            if (failOnCreate)
                throw new InvalidOperationException("MongoDB indisponível");
            return Task.CompletedTask;
        }

        public Task CompleteAsync(
            McpOperationJournal journal, object? resultSummary, CancellationToken cancellationToken = default)
        {
            events.Add("journal-complete");
            return Task.CompletedTask;
        }

        public Task FailAsync(
            McpOperationJournal journal, string errorCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
