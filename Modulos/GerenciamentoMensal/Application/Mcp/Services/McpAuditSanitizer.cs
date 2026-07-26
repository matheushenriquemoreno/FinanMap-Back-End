using System.Text.RegularExpressions;

namespace Application.Mcp.Services;

public sealed partial class McpAuditSanitizer
{
    private static readonly HashSet<string> AllowedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "tipo",
        "text",
        "limit",
        "cursor",
        "reasonCode",
        "status",
        "operationClass",
        "fromUtc",
        "toUtc"
    };

    public IReadOnlyDictionary<string, object?> Sanitize(IReadOnlyDictionary<string, object?> parameters)
    {
        return parameters
            .Where(item => AllowedFields.Contains(item.Key))
            .ToDictionary(
                item => item.Key,
                item => SanitizeValue(item.Value),
                StringComparer.OrdinalIgnoreCase);
    }

    private static object? SanitizeValue(object? value)
    {
        if (value is not string text)
            return value;

        if (SecretValuePattern().IsMatch(text))
            return "[REDACTED]";

        return text.Length <= 200 ? text : text[..200];
    }

    [GeneratedRegex(
        @"(?i)(bearer\s+[a-z0-9._~+/\-=]+|(?:token|secret|password|authorization_code|code_verifier)\s*[:=]\s*\S+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex SecretValuePattern();
}
