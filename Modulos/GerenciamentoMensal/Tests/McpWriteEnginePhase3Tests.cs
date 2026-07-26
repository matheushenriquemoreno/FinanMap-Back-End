using Domain.Mcp.Entities;
using Domain.Mcp.Enums;
using Application.Mcp.Services;
using System.Text;
using Xunit;

namespace Tests;

public sealed class McpWriteEnginePhase3Tests
{
    [Fact]
    public void Preview_reservation_is_single_use_and_bound_to_owner_hash_decision_and_expiration()
    {
        var now = new DateTime(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);
        var preview = McpPreview.Prepare(
            "owner-a",
            "connection-a",
            "finanmap_category_delete_preview",
            McpPreviewAction.Delete,
            "request-a",
            [1, 2, 3],
            "payload-hash",
            [new McpSnapshotHash("category", "category-a", "snapshot-a")],
            new Dictionary<string, object?>
            {
                ["entityType"] = "category",
                ["targetId"] = "category-a",
                ["irreversible"] = true
            },
            McpRequiredDecision.DeletePermanently,
            now,
            TimeSpan.FromMinutes(15),
            TimeSpan.FromHours(24));

        Assert.Equal(McpPreviewState.Prepared, preview.State);
        Assert.False(preview.TryReserve(
            "owner-b", "connection-a", "payload-hash",
            McpRequiredDecision.DeletePermanently, "operation-a", now.AddMinutes(1)));
        Assert.False(preview.TryReserve(
            "owner-a", "connection-a", "different-hash",
            McpRequiredDecision.DeletePermanently, "operation-a", now.AddMinutes(1)));
        Assert.False(preview.TryReserve(
            "owner-a", "connection-a", "payload-hash",
            McpRequiredDecision.ApplyChanges, "operation-a", now.AddMinutes(1)));
        Assert.False(preview.TryReserve(
            "owner-a", "connection-a", "payload-hash",
            McpRequiredDecision.DeletePermanently, "operation-a", now.AddMinutes(16)));
        Assert.Equal(McpPreviewState.Expired, preview.State);

        var fresh = McpPreview.Prepare(
            "owner-a",
            "connection-a",
            "finanmap_category_delete_preview",
            McpPreviewAction.Delete,
            "request-b",
            [4, 5, 6],
            "payload-hash-b",
            [],
            new Dictionary<string, object?>(),
            McpRequiredDecision.DeletePermanently,
            now,
            TimeSpan.FromMinutes(15),
            TimeSpan.FromHours(24));

        Assert.True(fresh.TryReserve(
            "owner-a", "connection-a", "payload-hash-b",
            McpRequiredDecision.DeletePermanently, "operation-b", now.AddMinutes(1)));
        Assert.False(fresh.TryReserve(
            "owner-a", "connection-a", "payload-hash-b",
            McpRequiredDecision.DeletePermanently, "operation-c", now.AddMinutes(1)));
        Assert.Equal("operation-b", fresh.OperationId);
        Assert.Equal(1, fresh.Version);
    }

    [Fact]
    public void Journal_tracks_preview_targets_steps_lease_attempts_and_honest_unknown_state()
    {
        var now = new DateTime(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);
        var journal = McpOperationJournal.Start(
            "owner-a",
            "connection-a",
            "correlation-a",
            "finanmap_operation_confirm",
            McpOperationClass.Confirm,
            idempotencyKey: "preview-a",
            requestHash: "confirmation-hash",
            previewId: "preview-a",
            targetRefs: [new McpTargetRef("category", "category-a")],
            steps:
            [
                new McpOperationStep(
                    "apply",
                    McpOperationStepState.Pending,
                    null,
                    null,
                    null)
            ],
            startedAtUtc: now);

        Assert.Equal("preview-a", journal.PreviewId);
        Assert.True(journal.TryAcquireLease(
            "worker-a", now, TimeSpan.FromMinutes(1)));
        Assert.Equal(McpOperationState.Executing, journal.State);
        Assert.Equal(1, journal.AttemptCount);
        Assert.False(journal.TryAcquireLease(
            "worker-b", now.AddSeconds(30), TimeSpan.FromMinutes(1)));
        Assert.True(journal.TryAcquireLease(
            "worker-b", now.AddMinutes(2), TimeSpan.FromMinutes(1)));
        Assert.Equal(2, journal.AttemptCount);

        journal.StartStep("apply", "worker-b", now.AddMinutes(2));
        journal.CompleteStep(
            "apply",
            "effect-marker-a",
            new Dictionary<string, object?> { ["entityId"] = "category-a" },
            now.AddMinutes(2).AddSeconds(1));
        journal.MarkUnknown(
            "EFFECT_OUTCOME_UNKNOWN",
            now.AddMinutes(2).AddSeconds(2));

        Assert.Equal(McpOperationState.Unknown, journal.State);
        Assert.Equal(
            McpOperationStepState.Completed,
            Assert.Single(journal.Steps).State);
        Assert.False(journal.TryAcquireLease(
            "worker-c", now.AddMinutes(4), TimeSpan.FromMinutes(1)));
        Assert.Null(journal.NextAttemptAtUtc);
        Assert.Contains("EFFECT_OUTCOME_UNKNOWN", journal.ErrorCodes);
    }

    [Fact]
    public void Preview_payload_protection_is_authenticated_and_never_stores_plaintext()
    {
        var protector = new McpPreviewPayloadProtector(
            Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
        const string plaintext =
            """{"description":"segredo-financeiro","amount":"100.00"}""";

        var ciphertext = protector.Protect(Encoding.UTF8.GetBytes(plaintext));

        Assert.DoesNotContain(
            "segredo-financeiro",
            Encoding.UTF8.GetString(ciphertext),
            StringComparison.Ordinal);
        Assert.Equal(
            plaintext,
            Encoding.UTF8.GetString(protector.Unprotect(ciphertext)));

        ciphertext[^1] ^= 0x01;
        Assert.ThrowsAny<Exception>(() =>
        {
            protector.Unprotect(ciphertext);
        });
    }
}
