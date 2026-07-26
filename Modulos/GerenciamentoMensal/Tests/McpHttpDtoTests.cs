using System.Text.Json;
using Application.Mcp.Configuration;
using Domain.Mcp.Entities;
using Domain.Mcp.Enums;
using WebApi.Mcp;
using Xunit;

namespace Tests;

public class McpHttpDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Connection_and_audit_enums_use_the_lower_camel_contract()
    {
        var connection = McpConnection.CreateActive(
            "owner-a", "authorization-a", "client-a", "Cliente", ["mcp:read"]);
        var journal = McpOperationJournal.Start(
            "owner-a", connection.Id, "correlation-a",
            "finanmap_categories_list", McpOperationClass.Read);
        journal.Complete();

        var connectionJson = JsonSerializer.Serialize(McpHttpDtoMapper.Map(connection), JsonOptions);
        var auditJson = JsonSerializer.Serialize(McpHttpDtoMapper.Map(journal), JsonOptions);

        Assert.Contains("\"status\":\"active\"", connectionJson);
        Assert.Contains("\"operationClass\":\"read\"", auditJson);
        Assert.Contains("\"state\":\"completed\"", auditJson);
    }

    [Fact]
    public void Configuration_contract_exposes_pinned_protocol_profiles_and_independent_features()
    {
        var options = new McpFeatureOptions
        {
            EndpointEnabled = true,
            WriteToolsEnabled = false,
            HistoryEnabled = true,
            PublicBaseUrl = "https://api.example"
        };

        var response = McpHttpDtoMapper.MapConfiguration(options);

        Assert.Equal("https://api.example/mcp", response.Endpoint);
        Assert.Equal("2025-11-25", response.ProtocolRevision);
        Assert.Equal("S256", response.Authorization.PkceMethod);
        Assert.Equal(["mcp:read", "mcp:audit"], response.Profiles[0].Scopes);
        Assert.True(response.Features.EndpointEnabled);
        Assert.False(response.Features.WriteToolsEnabled);
        Assert.True(response.Features.HistoryEnabled);
    }

    [Fact]
    public void OAuth_continuation_preserves_redirect_state_resource_and_pkce_challenge()
    {
        var interaction = new McpAuthorizationInteraction(
            "interaction-a",
            "client-a",
            "Cliente",
            ["mcp:read"],
            DateTime.UtcNow.AddMinutes(5),
            "https://agent.example/callback",
            "opaque-state",
            "pkce-challenge",
            "S256",
            "https://api.example/mcp");
        interaction.Approve("owner-a", ["mcp:read"], DateTime.UtcNow);
        var connection = McpConnection.CreateActive(
            "owner-a", "authorization-a", "client-a", "Cliente", ["mcp:read"]);
        interaction.BindConnection(connection.Id);

        var continuation = McpOAuthAuthorizeEndpoint.BuildContinuationUrl(
            new McpFeatureOptions { PublicBaseUrl = "https://api.example" },
            connection,
            interaction);

        Assert.Contains("redirect_uri=https%3A%2F%2Fagent.example%2Fcallback", continuation);
        Assert.Contains("state=opaque-state", continuation);
        Assert.Contains("code_challenge=pkce-challenge", continuation);
        Assert.Contains("code_challenge_method=S256", continuation);
        Assert.Contains("resource=https%3A%2F%2Fapi.example%2Fmcp", continuation);
        Assert.Contains("interaction_id=interaction-a", continuation);
    }

    [Fact]
    public void Denial_continuation_returns_registered_callback_with_state_and_no_code_or_token()
    {
        var interaction = new McpAuthorizationInteraction(
            "interaction-a",
            "client-a",
            "Cliente",
            ["mcp:read"],
            DateTime.UtcNow.AddMinutes(5),
            "https://agent.example/callback?source=inspector",
            "opaque-state",
            "pkce-challenge",
            "S256",
            "https://api.example/mcp");

        var continuation = McpOAuthAuthorizeEndpoint.BuildDeniedContinuationUrl(interaction);
        var uri = new Uri(continuation);

        Assert.Equal("https://agent.example/callback", uri.GetLeftPart(UriPartial.Path));
        Assert.Contains("source=inspector", uri.Query);
        Assert.Contains("error=access_denied", uri.Query);
        Assert.Contains("state=opaque-state", uri.Query);
        Assert.DoesNotContain("code=", uri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", uri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("challenge", uri.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Authorization_interaction_cannot_be_claimed_or_completed_by_another_owner()
    {
        var interaction = new McpAuthorizationInteraction(
            "interaction-a",
            "client-a",
            "Cliente",
            ["mcp:read"],
            DateTime.UtcNow.AddMinutes(5));
        interaction.Claim("owner-a", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            interaction.Deny("owner-b", DateTime.UtcNow));
    }
}
