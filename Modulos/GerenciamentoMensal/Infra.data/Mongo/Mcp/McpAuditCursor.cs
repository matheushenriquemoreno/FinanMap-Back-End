using System.Text;

namespace Infra.Data.Mongo.Mcp;

public static class McpAuditCursor
{
    public static string Encode(DateTime startedAtUtc, string id)
    {
        var payload = $"{startedAtUtc.ToUniversalTime().Ticks}:{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static McpAuditCursorValue Decode(string cursor)
    {
        try
        {
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
            var payload = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            var separator = payload.IndexOf(':');
            if (separator <= 0 ||
                !long.TryParse(payload[..separator], out var ticks) ||
                ticks <= 0 ||
                string.IsNullOrWhiteSpace(payload[(separator + 1)..]))
                throw new FormatException();

            return new McpAuditCursorValue(
                new DateTime(ticks, DateTimeKind.Utc),
                payload[(separator + 1)..]);
        }
        catch (Exception exception) when (exception is not FormatException)
        {
            throw new FormatException("Cursor MCP inválido.", exception);
        }
    }
}

public sealed record McpAuditCursorValue(DateTime StartedAtUtc, string Id);
