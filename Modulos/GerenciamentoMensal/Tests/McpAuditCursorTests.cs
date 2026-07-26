using Infra.Data.Mongo.Mcp;
using Xunit;

namespace Tests;

public class McpAuditCursorTests
{
    [Fact]
    public void Cursor_round_trips_timestamp_and_id_without_exposing_raw_value()
    {
        var timestamp = new DateTime(2026, 7, 25, 18, 30, 0, DateTimeKind.Utc);

        var encoded = McpAuditCursor.Encode(timestamp, "507f1f77bcf86cd799439011");
        var decoded = McpAuditCursor.Decode(encoded);

        Assert.DoesNotContain("507f1f77bcf86cd799439011", encoded);
        Assert.Equal(timestamp, decoded.StartedAtUtc);
        Assert.Equal("507f1f77bcf86cd799439011", decoded.Id);
    }

    [Fact]
    public void Invalid_cursor_is_rejected()
    {
        Assert.Throws<FormatException>(() => McpAuditCursor.Decode("not-a-valid-cursor"));
    }
}
