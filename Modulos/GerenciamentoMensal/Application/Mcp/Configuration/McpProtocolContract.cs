namespace Application.Mcp.Configuration;

public static class McpProtocolContract
{
    public const string Revision = "2025-11-25";
    public const string SdkVersion = "1.4.1";
    public const string OAuthGrant = "authorization_code";
    public const string PkceMethod = "S256";
    public const string SchemaVersion = "1.0";

    public static readonly string[] ReadOnlyScopes = ["mcp:read", "mcp:audit"];
    public static readonly string[] FullManagementScopes = ["mcp:read", "mcp:write", "mcp:import", "mcp:audit"];
}
