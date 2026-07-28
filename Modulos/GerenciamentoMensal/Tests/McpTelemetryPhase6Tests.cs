using System.Diagnostics.Metrics;
using Application.Mcp.Models;
using WebApi.Mcp;
using Xunit;

namespace Tests;

public sealed class McpTelemetryPhase6Tests
{
    [Fact]
    public void Telemetry_emits_only_bounded_operational_dimensions()
    {
        var observed = new List<(string Name, IReadOnlyDictionary<string, object?> Tags)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == McpTelemetry.MeterName)
                    meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
            observed.Add((instrument.Name, tags.ToArray().ToDictionary(
                item => item.Key,
                item => item.Value))));
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) =>
            observed.Add((instrument.Name, tags.ToArray().ToDictionary(
                item => item.Key,
                item => item.Value))));
        listener.Start();
        using var telemetry = new McpTelemetry();

        telemetry.RecordTransportRequest(403, 12.5);
        telemetry.RecordSecurityDenial("ORIGIN_FORBIDDEN");
        telemetry.RecordToolCall("read", "success", 8.2);
        telemetry.RecordJournalWriteFailure();
        telemetry.RecordConfirmationReplay();
        telemetry.RecordReconciliation(new McpReconciliationBatchResult(
            Scanned: 3,
            Completed: 1,
            Rejected: 0,
            Unknown: 1,
            Skipped: 1));
        listener.Dispose();
        var snapshot = observed.ToArray();

        Assert.Contains(snapshot, item =>
            item.Name == "mcp_transport_duration_ms");
        Assert.Contains(snapshot, item =>
            item.Name == "mcp_auth_denied_total" &&
            Equals(item.Tags["reason"], "origin_forbidden"));
        Assert.Contains(snapshot, item =>
            item.Name == "mcp_unknown_operations_total");
        Assert.Contains(snapshot, item =>
            item.Name == "mcp_tool_calls_total");
        Assert.Contains(snapshot, item =>
            item.Name == "mcp_tool_duration_ms" &&
            Equals(item.Tags["operation_class"], "read"));
        Assert.Contains(snapshot, item =>
            item.Name == "mcp_journal_write_failure_total");
        Assert.Contains(snapshot, item =>
            item.Name == "mcp_confirmation_replay_total");
        Assert.All(snapshot.SelectMany(item => item.Tags.Keys), key =>
            Assert.Contains(
                key,
                new[] { "outcome", "reason", "operation_class" }));
    }
}
