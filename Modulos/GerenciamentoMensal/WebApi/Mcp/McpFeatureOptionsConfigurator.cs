using Application.Mcp.Configuration;
using Microsoft.Extensions.Options;

namespace WebApi.Mcp;

public sealed class McpFeatureOptionsConfigurator(IConfiguration configuration)
    : IConfigureOptions<McpFeatureOptions>
{
    public void Configure(McpFeatureOptions options)
    {
        configuration.GetSection(McpFeatureOptions.SectionName).Bind(options);
        options.EndpointEnabled = ReadBool("MCP_FEATURE_ENABLED", options.EndpointEnabled);
        options.WriteToolsEnabled = ReadBool("MCP_WRITE_TOOLS_ENABLED", options.WriteToolsEnabled);
        options.HistoryEnabled = ReadBool("MCP_HISTORY_ENABLED", options.HistoryEnabled);
        options.PublicBaseUrl =
            configuration["MCP_PUBLIC_BASE_URL"] ?? options.PublicBaseUrl;
        if (string.IsNullOrWhiteSpace(options.PublicBaseUrl))
            options.PublicBaseUrl = "https://localhost";
        options.ConsentUrl =
            configuration["MCP_CONSENT_URL"] ??
            options.ConsentUrl;
        if (string.IsNullOrWhiteSpace(options.ConsentUrl))
        {
            options.ConsentUrl = (configuration["FRONT_END_URLS"] ?? string.Empty)
                .Split('&', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? string.Empty;
        }
        options.AllowedOrigins = ReadList("MCP_ALLOWED_ORIGINS", options.AllowedOrigins);
    }

    private static bool ReadBool(string name, bool fallback) =>
        bool.TryParse(Environment.GetEnvironmentVariable(name), out var value) ? value : fallback;

    private static string[] ReadList(string name, string[] fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Split([';', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }
}
