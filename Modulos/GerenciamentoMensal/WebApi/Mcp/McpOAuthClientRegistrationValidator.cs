using Application.Mcp.Configuration;

namespace WebApi.Mcp;

public static class McpOAuthClientRegistrationValidator
{
    public static bool IsAllowedRedirectUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            value.Contains('*', StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(uri.Fragment))
            return false;

        if (uri.Scheme == Uri.UriSchemeHttps)
            return true;

        return uri.Scheme == Uri.UriSchemeHttp &&
               (uri.IsLoopback ||
                string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
    }

    public static bool AreAllowedScopes(IEnumerable<string> scopes)
    {
        var allowed = McpProtocolContract.FullManagementScopes.ToHashSet(StringComparer.Ordinal);
        return scopes.All(allowed.Contains);
    }
}
