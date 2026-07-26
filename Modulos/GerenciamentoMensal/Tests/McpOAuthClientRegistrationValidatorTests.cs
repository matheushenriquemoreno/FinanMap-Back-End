using WebApi.Mcp;
using Xunit;

namespace Tests;

public class McpOAuthClientRegistrationValidatorTests
{
    [Theory]
    [InlineData("https://agent.example/callback", true)]
    [InlineData("http://127.0.0.1:43123/callback", true)]
    [InlineData("http://localhost:43123/callback", true)]
    [InlineData("http://agent.example/callback", false)]
    [InlineData("https://*.example/callback", false)]
    [InlineData("javascript:alert(1)", false)]
    public void Redirect_uri_policy_allows_https_or_native_loopback_only(string value, bool expected)
    {
        Assert.Equal(expected, McpOAuthClientRegistrationValidator.IsAllowedRedirectUri(value));
    }

    [Fact]
    public void Registration_rejects_privileged_or_unknown_scopes()
    {
        Assert.True(McpOAuthClientRegistrationValidator.AreAllowedScopes(["mcp:read", "mcp:audit"]));
        Assert.False(McpOAuthClientRegistrationValidator.AreAllowedScopes(["mcp:admin"]));
    }
}
