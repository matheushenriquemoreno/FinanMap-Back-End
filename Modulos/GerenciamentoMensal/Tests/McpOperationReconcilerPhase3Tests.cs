using System.Text.Json;
using Application.Mcp.Interfaces;
using Application.Mcp.Models;
using Application.Mcp.Services;
using Domain.Mcp.Entities;
using Domain.Mcp.Enums;
using Domain.Mcp.Repositories;
using Xunit;

namespace Tests;

public sealed class McpOperationReconcilerPhase3Tests
{
    [Fact]
    public async Task Reconciler_resumes_with_same_operation_id_when_absence_of_effect_is_proven()
    {
        var now = new DateTime(2026, 7, 27, 12, 2, 0, DateTimeKind.Utc);
        var protector = Protector();
        var command = new McpWriteCommand(
            McpWriteEntity.Category,
            McpPreviewAction.Create,
            null,
            new Dictionary<string, object?>
            {
                ["name"] = "Trabalho",
                ["type"] = "Despesa"
            });
        var preview = Preview(protector, command, now.AddMinutes(-2));
        var journal = Journal(preview, now.AddMinutes(-2));
        var repository = new RepositoryFake(preview, journal);
        var gateway = new GatewayFake
        {
            ExecutedEffect = McpDomainEffect.Completed(
                "category-created",
                journal.Id,
                new Dictionary<string, object?>
                {
                    ["entityType"] = "category",
                    ["entityId"] = "category-created",
                    ["action"] = "create"
                })
        };
        var reconciler = new McpOperationReconciler(
            gateway,
            repository,
            repository,
            protector,
            new FixedTimeProvider(now));

        var result = await reconciler.ReconcileDueAsync(10);

        Assert.Equal(1, result.Completed);
        Assert.Equal(journal.Id, gateway.LastOperationId);
        Assert.Equal(1, gateway.ExecuteCount);
        Assert.Equal(McpOperationState.Completed, journal.State);
        Assert.Equal(McpPreviewState.Completed, preview.State);
    }

    [Fact]
    public async Task Reconciler_never_reexecutes_delete_when_absence_has_uncertain_causality()
    {
        var now = new DateTime(2026, 7, 27, 12, 4, 0, DateTimeKind.Utc);
        var protector = Protector();
        var command = new McpWriteCommand(
            McpWriteEntity.Category,
            McpPreviewAction.Delete,
            "category-deleted",
            new Dictionary<string, object?>(),
            new Dictionary<string, object?>
            {
                ["name"] = "Antiga",
                ["type"] = "Despesa"
            });
        var preview = Preview(protector, command, now.AddMinutes(-4));
        var journal = Journal(preview, now.AddMinutes(-4));
        Assert.True(preview.TryReserve(
            "owner-a",
            "connection-a",
            preview.PayloadHash,
            preview.RequiredDecision,
            journal.Id,
            now.AddMinutes(-3)));
        Assert.True(journal.TryAcquireLease(
            "crashed-worker",
            now.AddMinutes(-3),
            TimeSpan.FromMinutes(1)));
        var repository = new RepositoryFake(preview, journal);
        var gateway = new GatewayFake
        {
            ObservedEffect = McpDomainEffect.Unknown(
                "O alvo está ausente e a causalidade não pode ser comprovada.")
        };
        var reconciler = new McpOperationReconciler(
            gateway,
            repository,
            repository,
            protector,
            new FixedTimeProvider(now));

        var result = await reconciler.ReconcileDueAsync(10);

        Assert.Equal(1, result.Unknown);
        Assert.Equal(0, gateway.ExecuteCount);
        Assert.Equal(McpOperationState.Unknown, journal.State);
        Assert.Equal(McpPreviewState.Unknown, preview.State);
    }

    [Fact]
    public async Task Reconciler_finalizes_observed_effect_marker_without_reexecuting_domain_effect()
    {
        var now = new DateTime(2026, 7, 27, 12, 4, 0, DateTimeKind.Utc);
        var protector = Protector();
        var command = new McpWriteCommand(
            McpWriteEntity.Income,
            McpPreviewAction.Update,
            "income-a",
            new Dictionary<string, object?>
            {
                ["amount"] = "150.00"
            },
            new Dictionary<string, object?>
            {
                ["amount"] = "100.00"
            });
        var preview = Preview(protector, command, now.AddMinutes(-4));
        var journal = Journal(preview, now.AddMinutes(-4));
        Assert.True(preview.TryReserve(
            "owner-a",
            "connection-a",
            preview.PayloadHash,
            preview.RequiredDecision,
            journal.Id,
            now.AddMinutes(-3)));
        Assert.True(journal.TryAcquireLease(
            "crashed-after-effect",
            now.AddMinutes(-3),
            TimeSpan.FromMinutes(1)));
        var repository = new RepositoryFake(preview, journal);
        var gateway = new GatewayFake
        {
            ObservedEffect = McpDomainEffect.Completed(
                "income-a",
                journal.Id,
                new Dictionary<string, object?>
                {
                    ["entityType"] = "income",
                    ["entityId"] = "income-a",
                    ["action"] = "update"
                })
        };
        var reconciler = new McpOperationReconciler(
            gateway,
            repository,
            repository,
            protector,
            new FixedTimeProvider(now));

        var result = await reconciler.ReconcileDueAsync(10);

        Assert.Equal(1, result.Completed);
        Assert.Equal(0, gateway.ExecuteCount);
        Assert.Equal(McpOperationState.Completed, journal.State);
        Assert.NotNull(journal.ReconciledAtUtc);
    }

    [Fact]
    public async Task Reconciler_resumes_only_pending_step_and_never_repeats_completed_step()
    {
        var now = new DateTime(2026, 7, 27, 12, 6, 0, DateTimeKind.Utc);
        var protector = Protector();
        var firstValues = new Dictionary<string, object?>
        {
            ["year"] = 2026,
            ["month"] = 7,
            ["description"] = "Parcela 1",
            ["amount"] = "50.00",
            ["categoryId"] = "category-a"
        };
        var secondValues = new Dictionary<string, object?>(firstValues)
        {
            ["month"] = 8,
            ["description"] = "Parcela 2"
        };
        var command = new McpWriteCommand(
            McpWriteEntity.Expense,
            McpPreviewAction.Create,
            null,
            firstValues,
            Steps:
            [
                new McpWriteStepPlan("expense-001", null, firstValues, null),
                new McpWriteStepPlan("expense-002", null, secondValues, null)
            ]);
        var preview = Preview(protector, command, now.AddMinutes(-4));
        var journal = McpOperationJournal.Start(
            preview.UserId,
            preview.ConnectionId,
            "correlation-multi",
            "finanmap_operation_confirm",
            McpOperationClass.Confirm,
            idempotencyKey: preview.Id,
            requestHash: "confirmation-hash",
            previewId: preview.Id,
            steps:
            [
                new McpOperationStep("expense-001", McpOperationStepState.Pending, null, null, null),
                new McpOperationStep("expense-002", McpOperationStepState.Pending, null, null, null)
            ],
            startedAtUtc: now.AddMinutes(-4));
        Assert.True(preview.TryReserve(
            preview.UserId,
            preview.ConnectionId,
            preview.PayloadHash,
            preview.RequiredDecision,
            journal.Id,
            now.AddMinutes(-3)));
        Assert.True(journal.TryAcquireLease(
            "crashed-worker",
            now.AddMinutes(-3),
            TimeSpan.FromMinutes(1)));
        journal.StartStep("expense-001", "crashed-worker", now.AddMinutes(-3));
        journal.CompleteStep(
            "expense-001",
            $"{journal.Id}:expense-001",
            new Dictionary<string, object?>
            {
                ["entityType"] = "expense",
                ["entityId"] = "expense-first",
                ["action"] = "create"
            },
            now.AddMinutes(-3));
        journal.StartStep("expense-002", "crashed-worker", now.AddMinutes(-3));
        journal.ScheduleReconciliation(now.AddMinutes(-1));
        var repository = new RepositoryFake(preview, journal);
        var gateway = new GatewayFake
        {
            ExecutedEffect = McpDomainEffect.Completed(
                "expense-second",
                $"{journal.Id}:expense-002",
                new Dictionary<string, object?>
                {
                    ["entityType"] = "expense",
                    ["entityId"] = "expense-second",
                    ["action"] = "create"
                })
        };
        var reconciler = new McpOperationReconciler(
            gateway,
            repository,
            repository,
            protector,
            new FixedTimeProvider(now));

        var result = await reconciler.ReconcileDueAsync(10);

        Assert.Equal(1, result.Completed);
        Assert.Equal(1, gateway.ExecuteCount);
        Assert.Equal($"{journal.Id}:expense-002", gateway.LastOperationId);
        Assert.Equal("Parcela 2", gateway.LastCommand!.Values["description"]?.ToString());
        Assert.All(journal.Steps, step => Assert.Equal(McpOperationStepState.Completed, step.State));
        Assert.Equal(McpOperationState.Completed, journal.State);
    }

    private static McpPreviewPayloadProtector Protector() =>
        new(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());

    private static McpPreview Preview(
        IMcpPreviewPayloadProtector protector,
        McpWriteCommand command,
        DateTime createdAtUtc) =>
        McpPreview.Prepare(
            "owner-a",
            "connection-a",
            "finanmap_category_create_preview",
            command.Action,
            $"request-{command.Action}",
            protector.Protect(JsonSerializer.SerializeToUtf8Bytes(command)),
            McpCursorCodec.CanonicalFingerprint(command),
            command.TargetId is null
                ? []
                : [new McpSnapshotHash("category", command.TargetId, "snapshot")],
            new Dictionary<string, object?>
            {
                ["resourceType"] = "category",
                ["recordReference"] = command.TargetId ?? "new",
                ["changes"] = Array.Empty<object>(),
                ["irreversible"] = command.Action == McpPreviewAction.Delete,
                ["requiredDecision"] = command.Action == McpPreviewAction.Delete
                    ? "DELETE_PERMANENTLY"
                    : "APPLY_CHANGES"
            },
            command.Action == McpPreviewAction.Delete
                ? McpRequiredDecision.DeletePermanently
                : McpRequiredDecision.ApplyChanges,
            createdAtUtc,
            TimeSpan.FromMinutes(15),
            TimeSpan.FromHours(24));

    private static McpOperationJournal Journal(
        McpPreview preview,
        DateTime startedAtUtc) =>
        McpOperationJournal.Start(
            preview.UserId,
            preview.ConnectionId,
            "correlation-a",
            "finanmap_operation_confirm",
            McpOperationClass.Confirm,
            idempotencyKey: preview.Id,
            requestHash: "confirmation-hash",
            previewId: preview.Id,
            steps:
            [
                new McpOperationStep(
                    "apply",
                    McpOperationStepState.Pending,
                    null,
                    null,
                    null)
            ],
            startedAtUtc: startedAtUtc);

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(utcNow, TimeSpan.Zero);
    }

    private sealed class GatewayFake : IMcpWriteDomainGateway
    {
        public McpDomainEffect? ObservedEffect { get; init; }
        public McpDomainEffect ExecutedEffect { get; init; } =
            McpDomainEffect.Unknown("Não configurado.");
        public int ExecuteCount { get; private set; }
        public string? LastOperationId { get; private set; }
        public McpWriteCommand? LastCommand { get; private set; }

        public Task<McpDomainPreparation> PrepareAsync(
            string userId,
            McpWriteCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<McpDomainEffect> ExecuteAsync(
            string userId,
            McpWriteCommand command,
            string operationId,
            IReadOnlyList<McpSnapshotHash> snapshots,
            CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            LastOperationId = operationId;
            LastCommand = command;
            return Task.FromResult(ExecutedEffect);
        }

        public Task<McpDomainEffect?> FindEffectAsync(
            string userId,
            McpWriteCommand command,
            string operationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ObservedEffect);
    }

    private sealed class RepositoryFake(
        McpPreview preview,
        McpOperationJournal journal)
        : IMcpPreviewRepository, IMcpConfirmationJournalRepository
    {
        public Task<McpPreviewCreateResult> CreateOrGetAsync(
            McpPreview candidate,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<McpPreview?> GetOwnedAsync(
            string previewId,
            string userId,
            string connectionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                preview.Id == previewId &&
                preview.UserId == userId &&
                preview.ConnectionId == connectionId
                    ? preview
                    : null);

        public Task<McpPreview?> GetByRequestAsync(
            string userId,
            string connectionId,
            string toolName,
            string requestId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<McpPreview?>(preview);

        public Task<McpPreview?> TryReserveAsync(
            string previewId,
            string userId,
            string connectionId,
            string payloadHash,
            McpRequiredDecision decision,
            string operationId,
            DateTime nowUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                preview.TryReserve(
                    userId,
                    connectionId,
                    payloadHash,
                    decision,
                    operationId,
                    nowUtc)
                    ? preview
                    : null);

        public Task<McpPreview?> TryCancelAsync(
            string previewId,
            string userId,
            string connectionId,
            DateTime nowUtc,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> ReplaceAsync(
            McpPreview candidate,
            int expectedVersion,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<McpJournalCreateResult> CreateOrGetAsync(
            McpOperationJournal candidate,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<McpOperationJournal?> GetOwnedAsync(
            string operationId,
            string userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                journal.Id == operationId && journal.UserId == userId
                    ? journal
                    : null);

        public Task<McpOperationJournal?> GetByPreviewAsync(
            string previewId,
            string userId,
            string connectionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<McpOperationJournal?>(journal);

        public Task<McpOperationJournal?> TryAcquireLeaseAsync(
            string operationId,
            string leaseOwner,
            DateTime nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                journal.TryAcquireLease(leaseOwner, nowUtc, leaseDuration)
                    ? journal
                    : null);

        public Task<bool> ReplaceAsync(
            McpOperationJournal candidate,
            int expectedVersion,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<IReadOnlyList<McpOperationJournal>> ListRecoverableAsync(
            DateTime nowUtc,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<McpOperationJournal>>([journal]);
    }
}
