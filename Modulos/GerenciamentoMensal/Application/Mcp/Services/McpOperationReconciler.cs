#nullable enable

using System.Text.Json;
using Application.Mcp.Interfaces;
using Application.Mcp.Models;
using Domain.Mcp.Entities;
using Domain.Mcp.Enums;
using Domain.Mcp.Repositories;

namespace Application.Mcp.Services;

public sealed class McpOperationReconciler(
    IMcpWriteDomainGateway domain,
    IMcpPreviewRepository previews,
    IMcpConfirmationJournalRepository operations,
    IMcpPreviewPayloadProtector protector,
    TimeProvider time)
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MaximumOperationalWindow = TimeSpan.FromMinutes(15);

    public async Task<McpReconciliationBatchResult> ReconcileDueAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var now = UtcNow();
        var due = await operations.ListRecoverableAsync(
            now,
            Math.Clamp(limit, 1, 200),
            cancellationToken);
        var completed = 0;
        var rejected = 0;
        var unknown = 0;
        var skipped = 0;

        foreach (var candidate in due)
        {
            var outcome = await ReconcileOneAsync(candidate, now, cancellationToken);
            switch (outcome)
            {
                case McpReconciliationOutcome.Completed:
                    completed++;
                    break;
                case McpReconciliationOutcome.Rejected:
                    rejected++;
                    break;
                case McpReconciliationOutcome.Unknown:
                    unknown++;
                    break;
                default:
                    skipped++;
                    break;
            }
        }

        return new McpReconciliationBatchResult(
            due.Count,
            completed,
            rejected,
            unknown,
            skipped);
    }

    private async Task<McpReconciliationOutcome> ReconcileOneAsync(
        McpOperationJournal candidate,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var leaseOwner = $"reconciler:{Guid.NewGuid():N}";
        var journal = await operations.TryAcquireLeaseAsync(
            candidate.Id,
            leaseOwner,
            now,
            LeaseDuration,
            cancellationToken);
        if (journal is null)
            return McpReconciliationOutcome.Skipped;

        var journalExpectedVersion = journal.Version;
        if (string.IsNullOrWhiteSpace(journal.PreviewId))
        {
            return await MarkUnknownAsync(
                journal,
                journalExpectedVersion,
                null,
                "PREVIEW_REFERENCE_MISSING",
                "A referência da prévia não está disponível para reconciliação.",
                now,
                cancellationToken);
        }

        var preview = await previews.GetOwnedAsync(
            journal.PreviewId,
            journal.UserId,
            journal.ConnectionId,
            cancellationToken);
        if (preview is null)
        {
            return await MarkUnknownAsync(
                journal,
                journalExpectedVersion,
                null,
                "PREVIEW_NOT_FOUND",
                "A prévia da operação não está disponível para reconciliação.",
                now,
                cancellationToken);
        }

        if (preview.State == McpPreviewState.Prepared)
        {
            var reserved = await previews.TryReserveAsync(
                preview.Id,
                journal.UserId,
                journal.ConnectionId,
                preview.PayloadHash,
                preview.RequiredDecision,
                journal.Id,
                now,
                cancellationToken);
            if (reserved is null)
            {
                return await RejectAsync(
                    journal,
                    journalExpectedVersion,
                    preview,
                    now >= preview.ExpiresAtUtc
                        ? "PREVIEW_EXPIRED"
                        : "PREVIEW_ALREADY_CONSUMED",
                    now,
                    cancellationToken);
            }
            preview = reserved;
        }
        else if (preview.State != McpPreviewState.Executing ||
                 !string.Equals(
                     preview.OperationId,
                     journal.Id,
                     StringComparison.Ordinal))
        {
            return await RejectAsync(
                journal,
                journalExpectedVersion,
                preview,
                preview.State == McpPreviewState.Expired
                    ? "PREVIEW_EXPIRED"
                    : "PREVIEW_ALREADY_CONSUMED",
                now,
                cancellationToken);
        }

        McpWriteCommand command;
        try
        {
            command = Deserialize(preview);
        }
        catch
        {
            return await MarkUnknownAsync(
                journal,
                journalExpectedVersion,
                preview,
                "PREVIEW_PAYLOAD_UNAVAILABLE",
                "O payload protegido não pôde ser recuperado para reconciliação.",
                now,
                cancellationToken);
        }

        var plans = command.Steps is { Count: > 0 }
            ? command.Steps
            :
            [
                new McpWriteStepPlan(
                    "apply",
                    command.TargetId,
                    command.Values,
                    command.ExpectedValues)
            ];
        foreach (var plan in plans)
        {
            var persistedStep = journal.Steps.SingleOrDefault(item =>
                string.Equals(item.Name, plan.Name, StringComparison.Ordinal));
            if (persistedStep is null)
            {
                return await MarkUnknownAsync(
                    journal,
                    journal.Version,
                    preview,
                    "JOURNAL_STEP_MISSING",
                    "O plano protegido não corresponde aos passos persistidos.",
                    now,
                    cancellationToken);
            }
            if (persistedStep.State == McpOperationStepState.Completed)
                continue;

            var stepCommand = CommandForStep(command, plan);
            var operationId = plans.Count == 1
                ? journal.Id
                : $"{journal.Id}:{plan.Name}";
            McpDomainEffect? effect;
            try
            {
                effect = await domain.FindEffectAsync(
                    journal.UserId,
                    stepCommand,
                    operationId,
                    cancellationToken);
            }
            catch
            {
                effect = McpDomainEffect.Unknown(
                    "A consulta do marcador de efeito falhou durante a reconciliação.");
            }

            if (effect is null &&
                now - journal.StartedAtUtc <= MaximumOperationalWindow)
            {
                var expectedBeforeStart = journal.Version;
                journal.StartStep(plan.Name, leaseOwner, now);
                if (!await operations.ReplaceAsync(
                        journal,
                        expectedBeforeStart,
                        cancellationToken))
                    return McpReconciliationOutcome.Skipped;
                try
                {
                    effect = await domain.ExecuteAsync(
                        journal.UserId,
                        stepCommand,
                        operationId,
                        preview.SnapshotHashes,
                        cancellationToken);
                }
                catch
                {
                    effect = McpDomainEffect.Unknown(
                        "O resultado permaneceu inconclusivo durante a reconciliação.");
                }
            }

            effect ??= McpDomainEffect.Unknown(
                "A janela operacional terminou sem prova suficiente do efeito.");
            var isLast = ReferenceEquals(plan, plans[^1]);
            if (effect.State == McpDomainEffectState.Completed && !isLast)
            {
                var expectedBeforeComplete = journal.Version;
                journal.EnsureTargetRef(
                    EntityWire(command.Entity),
                    effect.EntityId!);
                journal.CompleteStep(
                    plan.Name,
                    effect.EffectMarker ?? operationId,
                    effect.Result,
                    now);
                if (!await operations.ReplaceAsync(
                        journal,
                        expectedBeforeComplete,
                        cancellationToken))
                    return McpReconciliationOutcome.Skipped;
                continue;
            }

            return await PersistOutcomeAsync(
                journal,
                journal.Version,
                preview,
                stepCommand,
                plan.Name,
                effect,
                now,
                cancellationToken);
        }

        return await MarkUnknownAsync(
            journal,
            journal.Version,
            preview,
            "JOURNAL_ALREADY_APPLIED",
            "Todos os passos estavam concluídos, mas o journal não havia sido finalizado.",
            now,
            cancellationToken);
    }

    private async Task<McpReconciliationOutcome> PersistOutcomeAsync(
        McpOperationJournal journal,
        int journalExpectedVersion,
        McpPreview preview,
        McpWriteCommand command,
        string stepName,
        McpDomainEffect effect,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var previewExpectedVersion = preview.Version;
        if (effect.State == McpDomainEffectState.Completed)
        {
            journal.CompleteStep(
                stepName,
                effect.EffectMarker ?? journal.Id,
                effect.Result,
                now);
            journal.CompleteAt(
                new Dictionary<string, object?>
                {
                    ["status"] = "success",
                    ["entityType"] = EntityWire(command.Entity),
                    ["entityId"] = effect.EntityId,
                    ["action"] = command.Action.ToString().ToLowerInvariant(),
                    ["summary"] = "O efeito da operação foi comprovado pela reconciliação.",
                    ["preview"] = PreviewSummary(preview),
                    ["confirmation"] = ConfirmationSummary(preview),
                    ["reconciliation"] = new Dictionary<string, object?>
                    {
                        ["status"] = "completed",
                        ["summary"] = "O efeito foi comprovado ou retomado com o mesmo operationId."
                    }
                },
                now,
                reconciled: true);
            if (!await operations.ReplaceAsync(
                    journal,
                    journalExpectedVersion,
                    cancellationToken))
            {
                return McpReconciliationOutcome.Skipped;
            }
            preview.Complete();
            await previews.ReplaceAsync(
                preview,
                previewExpectedVersion,
                cancellationToken);
            return McpReconciliationOutcome.Completed;
        }

        if (effect.State is McpDomainEffectState.Rejected or
            McpDomainEffectState.ConflictChanged)
        {
            return await RejectAsync(
                journal,
                journalExpectedVersion,
                preview,
                effect.ErrorCode ?? "DOMAIN_REJECTED",
                now,
                cancellationToken,
                stepName);
        }

        return await MarkUnknownAsync(
            journal,
            journalExpectedVersion,
            preview,
            effect.ErrorCode ?? "EFFECT_OUTCOME_UNKNOWN",
            effect.Message ?? "O efeito permaneceu inconclusivo.",
            now,
            cancellationToken,
            stepName);
    }

    private async Task<McpReconciliationOutcome> RejectAsync(
        McpOperationJournal journal,
        int journalExpectedVersion,
        McpPreview preview,
        string errorCode,
        DateTime now,
        CancellationToken cancellationToken,
        string? stepName = null)
    {
        var previewExpectedVersion = preview.Version;
        var failingStep = stepName ?? PendingStepName(journal);
        if (failingStep is not null)
            journal.FailStep(failingStep, errorCode, false, now);
        journal.SetResultSummary(new Dictionary<string, object?>
        {
            ["action"] = PreviewText(preview, "action"),
            ["entityType"] = PreviewText(preview, "resourceType"),
            ["summary"] = "A operação foi encerrada sem efeito pendente.",
            ["preview"] = PreviewSummary(preview),
            ["confirmation"] = ConfirmationSummary(preview),
            ["reconciliation"] = new Dictionary<string, object?>
            {
                ["status"] = "rejected",
                ["summary"] = "A retomada segura foi rejeitada."
            },
            ["failure"] = new Dictionary<string, object?>
            {
                ["code"] = errorCode,
                ["message"] = "A operação não pôde ser retomada com segurança.",
                ["guidance"] = "Prepare uma nova prévia se a alteração ainda for necessária."
            }
        });
        journal.FailAt(errorCode, now);
        if (!await operations.ReplaceAsync(
                journal,
                journalExpectedVersion,
                cancellationToken))
        {
            return McpReconciliationOutcome.Skipped;
        }
        preview.Fail(false);
        await previews.ReplaceAsync(
            preview,
            previewExpectedVersion,
            cancellationToken);
        return McpReconciliationOutcome.Rejected;
    }

    private async Task<McpReconciliationOutcome> MarkUnknownAsync(
        McpOperationJournal journal,
        int journalExpectedVersion,
        McpPreview? preview,
        string errorCode,
        string message,
        DateTime now,
        CancellationToken cancellationToken,
        string? stepName = null)
    {
        var previewExpectedVersion = preview?.Version;
        var failingStep = stepName ?? PendingStepName(journal);
        if (failingStep is not null)
            journal.FailStep(failingStep, errorCode, true, now);
        journal.SetResultSummary(new Dictionary<string, object?>
        {
            ["action"] = preview is null ? null : PreviewText(preview, "action"),
            ["entityType"] = preview is null
                ? null
                : PreviewText(preview, "resourceType"),
            ["summary"] = "O resultado da escrita permaneceu desconhecido.",
            ["preview"] = preview is null ? null : PreviewSummary(preview),
            ["confirmation"] = preview is null ? null : ConfirmationSummary(preview),
            ["reconciliation"] = new Dictionary<string, object?>
            {
                ["status"] = "unknown",
                ["summary"] = message,
                ["guidance"] = "Não repita a escrita; use o operationId para suporte."
            },
            ["failure"] = new Dictionary<string, object?>
            {
                ["code"] = errorCode,
                ["message"] = message,
                ["guidance"] = "Não tente novamente automaticamente."
            }
        });
        journal.MarkUnknown(errorCode, now);
        if (!await operations.ReplaceAsync(
                journal,
                journalExpectedVersion,
                cancellationToken))
        {
            return McpReconciliationOutcome.Skipped;
        }
        if (preview is not null && previewExpectedVersion.HasValue)
        {
            preview.Fail(true);
            await previews.ReplaceAsync(
                preview,
                previewExpectedVersion.Value,
                cancellationToken);
        }
        return McpReconciliationOutcome.Unknown;
    }

    private static string? PreviewText(McpPreview preview, string key) =>
        preview.SafeSummary.TryGetValue(key, out var value)
            ? value?.ToString()
            : null;

    private McpWriteCommand Deserialize(McpPreview preview)
    {
        var plaintext = protector.Unprotect(preview.PayloadCiphertext);
        return JsonSerializer.Deserialize<McpWriteCommand>(plaintext)
               ?? throw new InvalidOperationException("Payload MCP inválido.");
    }

    private static IReadOnlyDictionary<string, object?> PreviewSummary(
        McpPreview preview) =>
        new Dictionary<string, object?>(preview.SafeSummary)
        {
            ["expiresAtUtc"] = preview.ExpiresAtUtc
        };

    private static IReadOnlyDictionary<string, object?> ConfirmationSummary(
        McpPreview preview) =>
        new Dictionary<string, object?>
        {
            ["decision"] = preview.RequiredDecision switch
            {
                McpRequiredDecision.ApplyChanges => "APPLY_CHANGES",
                McpRequiredDecision.DeletePermanently => "DELETE_PERMANENTLY",
                McpRequiredDecision.ImportValidItems => "IMPORT_VALID_ITEMS",
                _ => throw new ArgumentOutOfRangeException(nameof(preview))
            },
            ["confirmedAtUtc"] = preview.ConsumedAtUtc
        };

    private static McpWriteCommand CommandForStep(
        McpWriteCommand command,
        McpWriteStepPlan step) =>
        command with
        {
            TargetId = step.TargetId,
            Values = step.Values,
            ExpectedValues = step.ExpectedValues,
            Steps = null,
            StepType = step.Type
        };

    private static string? PendingStepName(McpOperationJournal journal) =>
        journal.Steps.FirstOrDefault(item =>
            item.State != McpOperationStepState.Completed)?.Name;

    private static string EntityWire(McpWriteEntity entity) =>
        entity switch
        {
            McpWriteEntity.Category => "category",
            McpWriteEntity.Income => "income",
            McpWriteEntity.Expense => "expense",
            McpWriteEntity.Investment => "investment",
            McpWriteEntity.FixedCost => "fixed_cost",
            _ => throw new ArgumentOutOfRangeException(nameof(entity))
        };

    private DateTime UtcNow() => time.GetUtcNow().UtcDateTime;

    private enum McpReconciliationOutcome
    {
        Completed,
        Rejected,
        Unknown,
        Skipped
    }
}
