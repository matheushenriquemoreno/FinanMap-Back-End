using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Mcp.Models;

namespace WebApi.Mcp;

public sealed class McpTelemetry : IDisposable
{
    public const string MeterName = "FinanMap.Mcp";

    private readonly Meter _meter = new(MeterName, "1.0.0");
    private readonly Histogram<double> _transportDuration;
    private readonly Counter<long> _authDenied;
    private readonly Counter<long> _reconciliationItems;
    private readonly Counter<long> _unknownOperations;
    private readonly Counter<long> _expiredLeases;
    private readonly Counter<long> _rateLimited;
    private readonly Counter<long> _toolCalls;
    private readonly Histogram<double> _toolDuration;
    private readonly Counter<long> _journalWriteFailures;
    private readonly Counter<long> _confirmationReplays;
    private readonly ConcurrentDictionary<string, byte> _confirmationAttempts =
        new(StringComparer.Ordinal);

    public McpTelemetry()
    {
        _transportDuration = _meter.CreateHistogram<double>(
            "mcp_transport_duration_ms",
            unit: "ms");
        _authDenied = _meter.CreateCounter<long>("mcp_auth_denied_total");
        _reconciliationItems = _meter.CreateCounter<long>(
            "mcp_reconciliation_items_total");
        _unknownOperations = _meter.CreateCounter<long>(
            "mcp_unknown_operations_total");
        _expiredLeases = _meter.CreateCounter<long>(
            "mcp_expired_leases_total");
        _rateLimited = _meter.CreateCounter<long>("mcp_rate_limited_total");
        _toolCalls = _meter.CreateCounter<long>("mcp_tool_calls_total");
        _toolDuration = _meter.CreateHistogram<double>(
            "mcp_tool_duration_ms",
            unit: "ms");
        _journalWriteFailures = _meter.CreateCounter<long>(
            "mcp_journal_write_failure_total");
        _confirmationReplays = _meter.CreateCounter<long>(
            "mcp_confirmation_replay_total");
    }

    public void RecordTransportRequest(int statusCode, double durationMilliseconds)
    {
        _transportDuration.Record(
            durationMilliseconds,
            new KeyValuePair<string, object?>(
                "outcome",
                statusCode < 400 ? "success" : "error"));
    }

    public void RecordSecurityDenial(string reason)
    {
        _authDenied.Add(
            1,
            new KeyValuePair<string, object?>(
                "reason",
                NormalizeReason(reason)));
    }

    public void RecordRateLimited()
    {
        _rateLimited.Add(
            1,
            new KeyValuePair<string, object?>("outcome", "rejected"));
    }

    public void RecordToolCall(
        string operationClass,
        string outcome,
        double durationMilliseconds)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>(
                "operation_class",
                operationClass),
            new KeyValuePair<string, object?>("outcome", outcome)
        };
        _toolCalls.Add(1, tags);
        _toolDuration.Record(durationMilliseconds, tags);
    }

    public void RecordJournalWriteFailure()
    {
        _journalWriteFailures.Add(
            1,
            new KeyValuePair<string, object?>("outcome", "error"));
    }

    public void RecordConfirmationReplay()
    {
        _confirmationReplays.Add(
            1,
            new KeyValuePair<string, object?>("outcome", "replayed"));
    }

    public void TrackConfirmationAttempt(
        string? toolName,
        IDictionary<string, JsonElement>? arguments)
    {
        if (toolName is not (
                "finanmap_operation_confirm" or
                "finanmap_import_confirm") ||
            arguments is null)
        {
            return;
        }

        var fingerprint = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(
                $"{toolName}:{JsonSerializer.Serialize(arguments)}")));
        if (!_confirmationAttempts.TryAdd(fingerprint, 0))
            RecordConfirmationReplay();
        if (_confirmationAttempts.Count > 4096)
            _confirmationAttempts.Clear();
    }

    public void RecordReconciliation(McpReconciliationBatchResult result)
    {
        RecordReconciliationOutcome("completed", result.Completed);
        RecordReconciliationOutcome("rejected", result.Rejected);
        RecordReconciliationOutcome("unknown", result.Unknown);
        RecordReconciliationOutcome("skipped", result.Skipped);
        if (result.Unknown > 0)
        {
            _unknownOperations.Add(
                result.Unknown,
                new KeyValuePair<string, object?>("outcome", "unknown"));
        }
        if (result.Scanned > 0)
        {
            _expiredLeases.Add(
                result.Scanned,
                new KeyValuePair<string, object?>("outcome", "scanned"));
        }
    }

    private void RecordReconciliationOutcome(string outcome, int count)
    {
        if (count <= 0)
            return;
        _reconciliationItems.Add(
            count,
            new KeyValuePair<string, object?>("outcome", outcome));
    }

    private static string NormalizeReason(string reason) =>
        reason.Trim().ToLowerInvariant().Replace('-', '_');

    public void Dispose() => _meter.Dispose();
}
