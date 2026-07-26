namespace Application.Mcp.Configuration;

public sealed class McpFeatureOptions
{
    public const string SectionName = "Mcp";

    public bool EndpointEnabled { get; set; }
    public bool WriteToolsEnabled { get; set; }
    public bool HistoryEnabled { get; set; }
    public string PublicBaseUrl { get; set; } = string.Empty;
    public string ConsentUrl { get; set; } = string.Empty;
    public string[] AllowedOrigins { get; set; } = [];
}
