#nullable enable

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Application.Mcp.Services;

public sealed class McpCursorCodec
{
    private readonly byte[] _key;

    public McpCursorCodec(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length < 32)
            throw new ArgumentException("A chave do cursor deve ter pelo menos 32 bytes.", nameof(key));
        _key = key.ToArray();
    }

    public string Encode(
        int offset,
        string userId,
        string toolName,
        string filtersFingerprint,
        string snapshotFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotFingerprint);
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new CursorPayload(
                offset,
                Fingerprint(userId),
                toolName,
                filtersFingerprint,
                snapshotFingerprint));
        var signature = HMACSHA256.HashData(_key, payload);
        return $"{Base64Url(payload)}.{Base64Url(signature)}";
    }

    public bool TryDecode(
        string? cursor,
        string userId,
        string toolName,
        string filtersFingerprint,
        out int offset,
        out string? snapshotFingerprint)
    {
        offset = 0;
        snapshotFingerprint = null;
        if (string.IsNullOrWhiteSpace(cursor))
            return true;

        try
        {
            var parts = cursor.Split('.');
            if (parts.Length != 2)
                return false;
            var payload = FromBase64Url(parts[0]);
            var signature = FromBase64Url(parts[1]);
            var expected = HMACSHA256.HashData(_key, payload);
            if (!CryptographicOperations.FixedTimeEquals(signature, expected))
                return false;
            var decoded = JsonSerializer.Deserialize<CursorPayload>(payload);
            if (decoded is null ||
                decoded.Offset < 0 ||
                !string.Equals(
                    decoded.UserFingerprint,
                    Fingerprint(userId),
                    StringComparison.Ordinal) ||
                !string.Equals(decoded.ToolName, toolName, StringComparison.Ordinal) ||
                !string.Equals(
                    decoded.FiltersFingerprint,
                    filtersFingerprint,
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(decoded.SnapshotFingerprint))
            {
                return false;
            }

            offset = decoded.Offset;
            snapshotFingerprint = decoded.SnapshotFingerprint;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string Fingerprint(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static string CanonicalFingerprint<T>(T value) =>
        Convert.ToHexString(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(value)));

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => string.Empty
        };
        return Convert.FromBase64String(padded);
    }

    private sealed record CursorPayload(
        int Offset,
        string UserFingerprint,
        string ToolName,
        string FiltersFingerprint,
        string SnapshotFingerprint);
}
