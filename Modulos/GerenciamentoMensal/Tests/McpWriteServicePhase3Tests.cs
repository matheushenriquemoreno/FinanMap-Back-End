using Application.Mcp.Interfaces;
using Application.Mcp.Models;
using Application.Mcp.Services;
using Domain.Enum;
using Domain.Mcp.Entities;
using Domain.Mcp.Enums;
using Domain.Mcp.Repositories;
using Xunit;

namespace Tests;

public sealed class McpWriteServicePhase3Tests
{
    [Fact]
    public async Task Preview_validates_clarifies_and_persists_protected_snapshot_without_effect()
    {
        var events = new List<string>();
        var gateway = new DomainGatewayFake(events)
        {
            Preparation = McpDomainPreparation.Ready(
                new McpWriteCommand(
                    McpWriteEntity.Category,
                    McpPreviewAction.Delete,
                    "category-a",
                    new Dictionary<string, object?>()),
                new Dictionary<string, object?>
                {
                    ["id"] = "category-a",
                    ["name"] = "Moradia",
                    ["type"] = "Despesa"
                },
                new Dictionary<string, object?>(),
                [new McpSnapshotHash("category", "category-a", "snapshot-a")])
        };
        var previews = new PreviewRepositoryFake(events);
        var journals = new JournalRepositoryFake(events);
        var service = CreateService(gateway, previews, journals, events);
        var context = Context();

        var clarification = await service.PrepareIncomeCreateAsync(
            context,
            new McpIncomeCreatePreviewInput(
                "request-invalid",
                2026,
                7,
                "Salário",
                "10.999",
                "category-a"));

        Assert.Equal("needs_clarification", clarification.Status);
        Assert.Equal("amount", Assert.Single(clarification.Errors).Field);
        Assert.Equal(0, gateway.PrepareCount);
        Assert.Equal(0, gateway.ExecuteCount);
        Assert.Equal(McpOperationState.Rejected, Assert.Single(journals.Items).State);

        events.Clear();
        var response = await service.PrepareCategoryDeleteAsync(
            context,
            new McpCategoryDeletePreviewInput("request-a", "category-a"));

        Assert.Equal("requires_confirmation", response.Status);
        Assert.True(response.Data!.Irreversible);
        Assert.Equal("DELETE_PERMANENTLY", response.Data.RequiredDecision);
        Assert.Equal("category-a", Assert.Single(response.Data.Targets).EntityId);
        Assert.Equal(
            ["confirm-journal", "connection", "prepare", "preview", "journal-save"],
            events);
        Assert.Equal(0, gateway.ExecuteCount);
        Assert.DoesNotContain(
            "Moradia",
            System.Text.Encoding.UTF8.GetString(
                previews.LastCreated!.PayloadCiphertext),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            journals.Items.Last().SanitizedParameters.Keys,
            key => key.Contains("name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Confirmation_is_exact_single_use_replays_result_and_rejects_changed_snapshot()
    {
        var events = new List<string>();
        var gateway = new DomainGatewayFake(events)
        {
            Preparation = McpDomainPreparation.Ready(
                new McpWriteCommand(
                    McpWriteEntity.Category,
                    McpPreviewAction.Create,
                    null,
                    new Dictionary<string, object?>
                    {
                        ["name"] = "Lazer",
                        ["type"] = "Despesa"
                    }),
                null,
                new Dictionary<string, object?>
                {
                    ["name"] = "Lazer",
                    ["type"] = "Despesa"
                },
                []),
            Effect = McpDomainEffect.Completed(
                "category-created",
                "effect-marker-a",
                new Dictionary<string, object?>
                {
                    ["entityType"] = "category",
                    ["entityId"] = "category-created",
                    ["action"] = "create"
                })
        };
        var previews = new PreviewRepositoryFake(events);
        var journals = new JournalRepositoryFake(events);
        var service = CreateService(gateway, previews, journals, events);
        var context = Context();
        var prepared = await service.PrepareCategoryCreateAsync(
            context,
            new McpCategoryCreatePreviewInput(
                "request-create",
                "Lazer",
                TipoCategoria.Despesa));
        events.Clear();

        var invalid = await service.ConfirmAsync(
            context,
            new McpOperationConfirmInput(
                prepared.Data!.PreviewId,
                prepared.Data.PayloadHash,
                "DELETE_PERMANENTLY"));
        Assert.Equal("rejected", invalid.Status);
        Assert.Equal("CONFIRMATION_DECISION_INVALID", Assert.Single(invalid.Errors).Code);
        Assert.Equal(0, gateway.ExecuteCount);

        events.Clear();
        var confirmed = await service.ConfirmAsync(
            context,
            new McpOperationConfirmInput(
                prepared.Data.PreviewId,
                prepared.Data.PayloadHash,
                "APPLY_CHANGES"));
        var replay = await service.ConfirmAsync(
            context,
            new McpOperationConfirmInput(
                prepared.Data.PreviewId,
                prepared.Data.PayloadHash,
                "APPLY_CHANGES"));

        Assert.Equal("success", confirmed.Status);
        Assert.Equal(confirmed.Data!.OperationId, replay.Data!.OperationId);
        Assert.Equal("category-created", confirmed.Data.EntityId);
        Assert.Equal(1, gateway.ExecuteCount);
        Assert.True(events.IndexOf("confirm-journal") < events.IndexOf("execute"));

        gateway.Effect = McpDomainEffect.ConflictChanged();
        gateway.Preparation = McpDomainPreparation.Ready(
            gateway.Preparation.Command with
            {
                Action = McpPreviewAction.Update,
                TargetId = "category-a"
            },
            new Dictionary<string, object?> { ["name"] = "Antes" },
            new Dictionary<string, object?> { ["name"] = "Depois" },
            [new McpSnapshotHash("category", "category-a", "old-hash")]);
        var changedPreview = await service.PrepareCategoryUpdateAsync(
            context,
            new McpCategoryUpdatePreviewInput(
                "request-update",
                "category-a",
                "Depois"));
        var changed = await service.ConfirmAsync(
            context,
            new McpOperationConfirmInput(
                changedPreview.Data!.PreviewId,
                changedPreview.Data.PayloadHash,
                "APPLY_CHANGES"));

        Assert.Equal("rejected", changed.Status);
        Assert.Equal("CONFLICT_CHANGED", Assert.Single(changed.Errors).Code);
    }

    [Fact]
    public async Task Generic_confirmation_requires_preview_id_and_executes_only_the_protected_command()
    {
        var events = new List<string>();
        var protectedValues = new Dictionary<string, object?>
        {
            ["name"] = "Original",
            ["type"] = "Despesa"
        };
        var gateway = new DomainGatewayFake(events)
        {
            Preparation = McpDomainPreparation.Ready(
                new McpWriteCommand(
                    McpWriteEntity.Category,
                    McpPreviewAction.Create,
                    null,
                    protectedValues),
                null,
                protectedValues,
                [])
        };
        var previews = new PreviewRepositoryFake(events);
        var journals = new JournalRepositoryFake(events);
        var service = CreateService(gateway, previews, journals, events);
        var context = Context();

        var empty = await service.ConfirmAsync(
            context,
            new McpOperationConfirmInput("", "hash", "APPLY_CHANGES"));
        Assert.Equal("rejected", empty.Status);
        Assert.Equal("previewId", Assert.Single(empty.Errors).Field);
        Assert.Equal(
            McpOperationState.Rejected,
            Assert.Single(journals.Items).State);

        var prepared = await service.PrepareCategoryCreateAsync(
            context,
            new McpCategoryCreatePreviewInput(
                "request-protected",
                "Original",
                TipoCategoria.Despesa));
        protectedValues["name"] = "MALICIOUS_MUTATION";
        gateway.Preparation = McpDomainPreparation.Ready(
            new McpWriteCommand(
                McpWriteEntity.Category,
                McpPreviewAction.Delete,
                "category-other",
                new Dictionary<string, object?>
                {
                    ["adminOverride"] = true
                }),
            null,
            new Dictionary<string, object?>(),
            []);

        var confirmed = await service.ConfirmAsync(
            context,
            new McpOperationConfirmInput(
                prepared.Data!.PreviewId,
                prepared.Data.PayloadHash,
                "APPLY_CHANGES"));

        Assert.Equal("success", confirmed.Status);
        Assert.Equal(McpPreviewAction.Create, gateway.LastExecutedCommand!.Action);
        Assert.Equal("Original", gateway.LastExecutedCommand.Values["name"]?.ToString());
        Assert.DoesNotContain("adminOverride", gateway.LastExecutedCommand.Values.Keys);
        var inputJson = System.Text.Json.JsonSerializer.Serialize(
            new McpOperationConfirmInput(
                prepared.Data.PreviewId,
                prepared.Data.PayloadHash,
                "APPLY_CHANGES"));
        Assert.DoesNotContain("values", inputJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("command", inputJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("target", inputJson, StringComparison.OrdinalIgnoreCase);
        var confirmationJournal = journals.Items.Single(item =>
            item.ToolName == "finanmap_operation_confirm" &&
            item.State == McpOperationState.Completed);
        var createdTarget = Assert.Single(confirmationJournal.TargetRefs);
        Assert.Equal("category", createdTarget.EntityType);
        Assert.Equal("entity-a", createdTarget.EntityId);
    }

    [Fact]
    public async Task Cancel_consumes_only_owned_prepared_preview_and_status_is_owner_scoped()
    {
        var events = new List<string>();
        var gateway = new DomainGatewayFake(events)
        {
            Preparation = McpDomainPreparation.Ready(
                new McpWriteCommand(
                    McpWriteEntity.Category,
                    McpPreviewAction.Create,
                    null,
                    new Dictionary<string, object?>
                    {
                        ["name"] = "Cancelável",
                        ["type"] = "Despesa"
                    }),
                null,
                new Dictionary<string, object?> { ["name"] = "Cancelável" },
                [])
        };
        var previews = new PreviewRepositoryFake(events);
        var journals = new JournalRepositoryFake(events);
        var service = CreateService(gateway, previews, journals, events);
        var context = Context();
        var prepared = await service.PrepareCategoryCreateAsync(
            context,
            new McpCategoryCreatePreviewInput(
                "request-cancel",
                "Cancelável",
                TipoCategoria.Despesa));

        var cancelled = await service.CancelAsync(
            context,
            new McpOperationCancelInput(prepared.Data!.PreviewId));
        var repeated = await service.CancelAsync(
            context,
            new McpOperationCancelInput(prepared.Data.PreviewId));

        Assert.Equal("success", cancelled.Status);
        Assert.Equal("cancelled", cancelled.Data!.State);
        Assert.Equal("rejected", repeated.Status);
        Assert.Equal(0, gateway.ExecuteCount);

        var missing = await service.GetStatusAsync(
            context,
            new McpOperationStatusInput("operation-other-owner"));
        Assert.Equal("rejected", missing.Status);
        Assert.Equal("OPERATION_NOT_FOUND", Assert.Single(missing.Errors).Code);
    }

    [Fact]
    public async Task Cancel_and_status_audit_every_invocation_before_validation_or_owner_checks()
    {
        var events = new List<string>();
        var gateway = new DomainGatewayFake(events)
        {
            Preparation = McpDomainPreparation.Ready(
                new McpWriteCommand(
                    McpWriteEntity.Category,
                    McpPreviewAction.Create,
                    null,
                    new Dictionary<string, object?>
                    {
                        ["name"] = "Auditável",
                        ["type"] = "Despesa"
                    }),
                null,
                new Dictionary<string, object?> { ["name"] = "Auditável" },
                [])
        };
        var previews = new PreviewRepositoryFake(events);
        var journals = new JournalRepositoryFake(events);
        var service = CreateService(gateway, previews, journals, events);
        var context = Context();
        var prepared = await service.PrepareCategoryCreateAsync(
            context,
            new McpCategoryCreatePreviewInput(
                "request-audit",
                "Auditável",
                TipoCategoria.Despesa));

        var blankCancel = await service.CancelAsync(
            context,
            new McpOperationCancelInput(""));
        var missingCancel = await service.CancelAsync(
            context,
            new McpOperationCancelInput("preview-other-owner"));
        var cancelled = await service.CancelAsync(
            context,
            new McpOperationCancelInput(prepared.Data!.PreviewId));
        var blankStatus = await service.GetStatusAsync(
            context,
            new McpOperationStatusInput(""));
        var missingStatus = await service.GetStatusAsync(
            context,
            new McpOperationStatusInput("operation-other-owner"));
        var foundStatus = await service.GetStatusAsync(
            context,
            new McpOperationStatusInput(cancelled.Data!.OperationId));

        Assert.Equal("rejected", blankCancel.Status);
        Assert.Equal("rejected", missingCancel.Status);
        Assert.Equal("success", cancelled.Status);
        Assert.Equal("rejected", blankStatus.Status);
        Assert.Equal("rejected", missingStatus.Status);
        Assert.Equal("success", foundStatus.Status);
        var invocationAudits = journals.Items
            .Where(item => item.ToolName is
                "finanmap_operation_cancel" or
                "finanmap_operation_status")
            .ToArray();
        Assert.Equal(6, invocationAudits.Length);
        Assert.Equal(
            [McpOperationState.Rejected, McpOperationState.Rejected, McpOperationState.Completed],
            invocationAudits
                .Where(item => item.ToolName == "finanmap_operation_cancel")
                .Select(item => item.State)
                .ToArray());
        Assert.Equal(
            [McpOperationState.Rejected, McpOperationState.Rejected, McpOperationState.Completed],
            invocationAudits
                .Where(item => item.ToolName == "finanmap_operation_status")
                .Select(item => item.State)
                .ToArray());
        var statusAuditJson = System.Text.Json.JsonSerializer.Serialize(
            invocationAudits.Where(item =>
                item.ToolName == "finanmap_operation_status"));
        Assert.DoesNotContain(
            "operation-other-owner",
            statusAuditJson,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            cancelled.Data.OperationId,
            statusAuditJson,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Category_and_income_create_update_delete_all_prepare_without_financial_effect()
    {
        var events = new List<string>();
        var gateway = new DomainGatewayFake(events)
        {
            Preparation = Ready(
                McpWriteEntity.Category,
                McpPreviewAction.Create)
        };
        var previews = new PreviewRepositoryFake(events);
        var journals = new JournalRepositoryFake(events);
        var service = CreateService(gateway, previews, journals, events);
        var context = Context();
        var calls = new Func<Task<McpToolEnvelope<McpPreviewData>>>[]
        {
            () => service.PrepareCategoryCreateAsync(
                context,
                new McpCategoryCreatePreviewInput(
                    "category-create",
                    "Nova",
                    TipoCategoria.Despesa)),
            () => service.PrepareCategoryUpdateAsync(
                context,
                new McpCategoryUpdatePreviewInput(
                    "category-update",
                    "category-a",
                    "Alterada")),
            () => service.PrepareCategoryDeleteAsync(
                context,
                new McpCategoryDeletePreviewInput(
                    "category-delete",
                    "category-a")),
            () => service.PrepareIncomeCreateAsync(
                context,
                new McpIncomeCreatePreviewInput(
                    "income-create",
                    2026,
                    7,
                    "Salário",
                    "1000.00",
                    "category-income")),
            () => service.PrepareIncomeUpdateAsync(
                context,
                new McpIncomeUpdatePreviewInput(
                    "income-update",
                    "income-a",
                    "Salário ajustado",
                    "1100.00",
                    "category-income")),
            () => service.PrepareIncomeDeleteAsync(
                context,
                new McpIncomeDeletePreviewInput(
                    "income-delete",
                    "income-a"))
        };
        var expected = new[]
        {
            (McpWriteEntity.Category, McpPreviewAction.Create),
            (McpWriteEntity.Category, McpPreviewAction.Update),
            (McpWriteEntity.Category, McpPreviewAction.Delete),
            (McpWriteEntity.Income, McpPreviewAction.Create),
            (McpWriteEntity.Income, McpPreviewAction.Update),
            (McpWriteEntity.Income, McpPreviewAction.Delete)
        };

        for (var index = 0; index < calls.Length; index++)
        {
            gateway.Preparation = Ready(expected[index].Item1, expected[index].Item2);
            var response = await calls[index]();
            Assert.Equal("requires_confirmation", response.Status);
            Assert.Equal(expected[index].Item1, gateway.LastPreparedCommand!.Entity);
            Assert.Equal(expected[index].Item2, gateway.LastPreparedCommand.Action);
        }

        Assert.Equal(6, gateway.PrepareCount);
        Assert.Equal(0, gateway.ExecuteCount);
    }

    [Fact]
    public async Task Required_field_and_financial_rule_matrix_rejects_invalid_category_and_income_inputs()
    {
        var events = new List<string>();
        var gateway = new DomainGatewayFake(events)
        {
            Preparation = Ready(McpWriteEntity.Category, McpPreviewAction.Create)
        };
        var service = CreateService(
            gateway,
            new PreviewRepositoryFake(events),
            new JournalRepositoryFake(events),
            events);
        var context = Context();

        var responses = new[]
        {
            await service.PrepareCategoryCreateAsync(
                context,
                new McpCategoryCreatePreviewInput("category-name", " ", TipoCategoria.Despesa)),
            await service.PrepareCategoryCreateAsync(
                context,
                new McpCategoryCreatePreviewInput("category-type", "Casa", (TipoCategoria)999)),
            await service.PrepareIncomeCreateAsync(
                context,
                new McpIncomeCreatePreviewInput(
                    "income-year", 2020, 7, "Salário", "100.00", "category-a")),
            await service.PrepareIncomeCreateAsync(
                context,
                new McpIncomeCreatePreviewInput(
                    "income-month", 2026, 13, "Salário", "100.00", "category-a")),
            await service.PrepareIncomeCreateAsync(
                context,
                new McpIncomeCreatePreviewInput(
                    "income-description", 2026, 7, " ", "100.00", "category-a")),
            await service.PrepareIncomeCreateAsync(
                context,
                new McpIncomeCreatePreviewInput(
                    "income-amount-zero", 2026, 7, "Salário", "0", "category-a")),
            await service.PrepareIncomeCreateAsync(
                context,
                new McpIncomeCreatePreviewInput(
                    "income-amount-negative", 2026, 7, "Salário", "-1", "category-a")),
            await service.PrepareIncomeCreateAsync(
                context,
                new McpIncomeCreatePreviewInput(
                    "income-amount-precision", 2026, 7, "Salário", "1.001", "category-a")),
            await service.PrepareIncomeCreateAsync(
                context,
                new McpIncomeCreatePreviewInput(
                    "income-category", 2026, 7, "Salário", "100.00", " "))
        };

        Assert.Equal(
            ["name", "type", "year", "month", "description", "amount", "amount", "amount", "categoryId"],
            responses.Select(response => Assert.Single(response.Errors).Field!).ToArray());
        Assert.All(
            responses,
            response => Assert.Equal("needs_clarification", response.Status));
        Assert.Equal(0, gateway.PrepareCount);
        Assert.Equal(0, gateway.ExecuteCount);
    }

    [Fact]
    public async Task Confirmation_rejects_missing_cross_account_expired_hash_and_decision_without_effect()
    {
        var events = new List<string>();
        var now = new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);
        var gateway = new DomainGatewayFake(events)
        {
            Preparation = Ready(
                McpWriteEntity.Category,
                McpPreviewAction.Create)
        };
        var previews = new PreviewRepositoryFake(events);
        var journals = new JournalRepositoryFake(events);
        var service = CreateService(
            gateway,
            previews,
            journals,
            events,
            new FixedTimeProvider(now));
        var context = Context();
        var prepared = await service.PrepareCategoryCreateAsync(
            context,
            new McpCategoryCreatePreviewInput(
                "confirmation-validation",
                "Nova",
                TipoCategoria.Despesa));

        var missing = await service.ConfirmAsync(
            context,
            new McpOperationConfirmInput(
                "preview-does-not-exist",
                prepared.Data!.PayloadHash,
                "APPLY_CHANGES"));
        var crossAccount = await service.ConfirmAsync(
            context with { UserId = "owner-b" },
            new McpOperationConfirmInput(
                prepared.Data.PreviewId,
                prepared.Data.PayloadHash,
                "APPLY_CHANGES"));
        var invalidHash = await service.ConfirmAsync(
            context,
            new McpOperationConfirmInput(
                prepared.Data.PreviewId,
                "different-hash",
                "APPLY_CHANGES"));
        var invalidDecision = await service.ConfirmAsync(
            context,
            new McpOperationConfirmInput(
                prepared.Data.PreviewId,
                prepared.Data.PayloadHash,
                "DELETE_PERMANENTLY"));
        var laterService = CreateService(
            gateway,
            previews,
            journals,
            events,
            new FixedTimeProvider(now.AddMinutes(16)));
        var expired = await laterService.ConfirmAsync(
            context,
            new McpOperationConfirmInput(
                prepared.Data.PreviewId,
                prepared.Data.PayloadHash,
                "APPLY_CHANGES"));

        Assert.Equal("PREVIEW_NOT_FOUND", Assert.Single(missing.Errors).Code);
        Assert.Equal("PREVIEW_NOT_FOUND", Assert.Single(crossAccount.Errors).Code);
        Assert.Equal("CONFIRMATION_HASH_INVALID", Assert.Single(invalidHash.Errors).Code);
        Assert.Equal(
            "CONFIRMATION_DECISION_INVALID",
            Assert.Single(invalidDecision.Errors).Code);
        Assert.Equal("PREVIEW_EXPIRED", Assert.Single(expired.Errors).Code);
        Assert.Equal(0, gateway.ExecuteCount);
    }

    [Fact]
    public async Task Journal_failure_blocks_effect_and_finalization_gap_never_claims_success()
    {
        var events = new List<string>();
        var gateway = new DomainGatewayFake(events)
        {
            Preparation = Ready(
                McpWriteEntity.Category,
                McpPreviewAction.Create),
            Effect = McpDomainEffect.Completed(
                "category-created",
                "marker-a",
                new Dictionary<string, object?>
                {
                    ["entityType"] = "category",
                    ["entityId"] = "category-created"
                })
        };
        var previews = new PreviewRepositoryFake(events);
        var journals = new JournalRepositoryFake(events) { FailAdd = true };
        var service = CreateService(gateway, previews, journals, events);

        await Assert.ThrowsAsync<McpJournalUnavailableException>(() =>
            service.PrepareCategoryCreateAsync(
                Context(),
                new McpCategoryCreatePreviewInput(
                    "journal-unavailable",
                    "Nova",
                    TipoCategoria.Despesa)));
        Assert.Equal(0, gateway.PrepareCount);
        Assert.Equal(0, gateway.ExecuteCount);

        journals.FailAdd = false;
        var prepared = await service.PrepareCategoryCreateAsync(
            Context(),
            new McpCategoryCreatePreviewInput(
                "finalization-gap",
                "Nova",
                TipoCategoria.Despesa));
        journals.FailReplace = true;
        var confirmation = await service.ConfirmAsync(
            Context(),
            new McpOperationConfirmInput(
                prepared.Data!.PreviewId,
                prepared.Data.PayloadHash,
                "APPLY_CHANGES"));

        Assert.Equal("unknown", confirmation.Status);
        Assert.Equal(
            "RESULT_PERSISTENCE_PENDING",
            Assert.Single(confirmation.Errors).Code);
        Assert.False(confirmation.Data!.RetryAllowed);
        Assert.Equal(1, gateway.ExecuteCount);
    }

    [Fact]
    public async Task Delete_receipt_and_safe_audit_remain_queryable_after_payload_is_purged()
    {
        var events = new List<string>();
        var gateway = new DomainGatewayFake(events)
        {
            Preparation = McpDomainPreparation.Ready(
                new McpWriteCommand(
                    McpWriteEntity.Category,
                    McpPreviewAction.Delete,
                    "category-a",
                    new Dictionary<string, object?>(),
                    new Dictionary<string, object?>
                    {
                        ["name"] = "Antiga",
                        ["type"] = "Despesa"
                    }),
                new Dictionary<string, object?>
                {
                    ["name"] = "Antiga",
                    ["type"] = "Despesa"
                },
                new Dictionary<string, object?>(),
                [new McpSnapshotHash("category", "category-a", "snapshot-a")]),
            Effect = McpDomainEffect.Completed(
                "category-a",
                "operation-delete",
                new Dictionary<string, object?>
                {
                    ["entityType"] = "category",
                    ["entityId"] = "category-a",
                    ["action"] = "delete"
                })
        };
        var previews = new PreviewRepositoryFake(events);
        var journals = new JournalRepositoryFake(events);
        var service = CreateService(gateway, previews, journals, events);
        var prepared = await service.PrepareCategoryDeleteAsync(
            Context(),
            new McpCategoryDeletePreviewInput("delete-audit", "category-a"));
        var confirmed = await service.ConfirmAsync(
            Context(),
            new McpOperationConfirmInput(
                prepared.Data!.PreviewId,
                prepared.Data.PayloadHash,
                "DELETE_PERMANENTLY"));
        var status = await service.GetStatusAsync(
            Context(),
            new McpOperationStatusInput(confirmed.Data!.OperationId));
        var journal = journals.Items.Single(item =>
            item.Id == confirmed.Data.OperationId);

        Assert.Equal("success", status.Status);
        Assert.Empty(previews.LastCreated!.PayloadCiphertext);
        Assert.Equal(McpOperationState.Completed, journal.State);
        Assert.True(journal.ResultSummary.ContainsKey("preview"));
        Assert.True(journal.ResultSummary.ContainsKey("confirmation"));
        Assert.Equal("category-a", Assert.Single(journal.TargetRefs).EntityId);
        Assert.DoesNotContain("PayloadCiphertext", journal.ResultSummary.Keys);
    }

    private static McpDomainPreparation Ready(
        McpWriteEntity entity,
        McpPreviewAction action)
    {
        var targetId = action == McpPreviewAction.Create
            ? null
            : entity == McpWriteEntity.Category
                ? "category-a"
                : "income-a";
        var proposed = entity == McpWriteEntity.Category
            ? new Dictionary<string, object?>
            {
                ["name"] = "Nova",
                ["type"] = "Despesa"
            }
            : new Dictionary<string, object?>
            {
                ["year"] = 2026,
                ["month"] = 7,
                ["description"] = "Salário",
                ["amount"] = "1000.00",
                ["categoryId"] = "category-income"
            };
        var current = action == McpPreviewAction.Create
            ? null
            : proposed;
        return McpDomainPreparation.Ready(
            new McpWriteCommand(
                entity,
                action,
                targetId,
                action == McpPreviewAction.Delete
                    ? new Dictionary<string, object?>()
                    : proposed,
                current),
            current,
            action == McpPreviewAction.Delete
                ? new Dictionary<string, object?>()
                : proposed,
            targetId is null
                ? []
                : [new McpSnapshotHash(
                    entity.ToString().ToLowerInvariant(),
                    targetId,
                    "snapshot")]);
    }

    private static McpWriteService CreateService(
        DomainGatewayFake gateway,
        PreviewRepositoryFake previews,
        JournalRepositoryFake journals,
        List<string> events,
        TimeProvider? time = null) =>
        new(
            gateway,
            new ConnectionValidatorFake(events),
            previews,
            journals,
            journals,
            new McpPreviewPayloadProtector(
                Enumerable.Range(1, 32).Select(value => (byte)value).ToArray()),
            time ?? new FixedTimeProvider(
                new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.Zero)),
            new McpAuditSanitizer());

    private static McpCallContext Context() =>
        new(
            "owner-a",
            "connection-a",
            "correlation-a",
            ClientId: "client-a");

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class ConnectionValidatorFake(List<string> events)
        : IMcpConnectionValidator
    {
        public Task<McpConnection> ValidateActiveAsync(
            string connectionId,
            string userId,
            string requiredScope,
            CancellationToken cancellationToken = default)
        {
            events.Add("connection");
            return Task.FromResult(McpConnection.CreateActive(
                userId,
                "authorization-a",
                "client-a",
                "Cliente A",
                [requiredScope]));
        }
    }

    private sealed class DomainGatewayFake(List<string> events)
        : IMcpWriteDomainGateway
    {
        public required McpDomainPreparation Preparation { get; set; }
        public McpDomainEffect Effect { get; set; } =
            McpDomainEffect.Completed(
                "entity-a",
                "marker-a",
                new Dictionary<string, object?>());
        public int PrepareCount { get; private set; }
        public int ExecuteCount { get; private set; }
        public McpWriteCommand? LastPreparedCommand { get; private set; }
        public McpWriteCommand? LastExecutedCommand { get; private set; }

        public Task<McpDomainPreparation> PrepareAsync(
            string userId,
            McpWriteCommand command,
            CancellationToken cancellationToken = default)
        {
            events.Add("prepare");
            PrepareCount++;
            LastPreparedCommand = command;
            return Task.FromResult(Preparation);
        }

        public Task<McpDomainEffect> ExecuteAsync(
            string userId,
            McpWriteCommand command,
            string operationId,
            IReadOnlyList<McpSnapshotHash> snapshots,
            CancellationToken cancellationToken = default)
        {
            events.Add("execute");
            ExecuteCount++;
            LastExecutedCommand = command;
            return Task.FromResult(Effect);
        }

        public Task<McpDomainEffect?> FindEffectAsync(
            string userId,
            McpWriteCommand command,
            string operationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<McpDomainEffect?>(null);
    }

    private sealed class PreviewRepositoryFake(List<string> events)
        : IMcpPreviewRepository
    {
        private readonly Dictionary<string, McpPreview> _items = [];
        public McpPreview? LastCreated { get; private set; }

        public Task<McpPreviewCreateResult> CreateOrGetAsync(
            McpPreview preview,
            CancellationToken cancellationToken = default)
        {
            events.Add("preview");
            var existing = _items.Values.FirstOrDefault(item =>
                item.UserId == preview.UserId &&
                item.ConnectionId == preview.ConnectionId &&
                item.ToolName == preview.ToolName &&
                item.RequestId == preview.RequestId);
            if (existing is not null)
            {
                return Task.FromResult(new McpPreviewCreateResult(
                    existing,
                    false,
                    existing.PayloadHash != preview.PayloadHash));
            }
            _items[preview.Id] = preview;
            LastCreated = preview;
            return Task.FromResult(new McpPreviewCreateResult(preview, true, false));
        }

        public Task<McpPreview?> GetOwnedAsync(
            string previewId,
            string userId,
            string connectionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.GetValueOrDefault(previewId) is { } item &&
                            item.UserId == userId &&
                            item.ConnectionId == connectionId
                ? item
                : null);

        public Task<McpPreview?> GetByRequestAsync(
            string userId,
            string connectionId,
            string toolName,
            string requestId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.Values.FirstOrDefault(item =>
                item.UserId == userId &&
                item.ConnectionId == connectionId &&
                item.ToolName == toolName &&
                item.RequestId == requestId));

        public Task<McpPreview?> TryReserveAsync(
            string previewId,
            string userId,
            string connectionId,
            string payloadHash,
            McpRequiredDecision decision,
            string operationId,
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            var item = _items.GetValueOrDefault(previewId);
            return Task.FromResult(item is not null &&
                                   item.TryReserve(
                                       userId,
                                       connectionId,
                                       payloadHash,
                                       decision,
                                       operationId,
                                       nowUtc)
                ? item
                : null);
        }

        public Task<McpPreview?> TryCancelAsync(
            string previewId,
            string userId,
            string connectionId,
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            var item = _items.GetValueOrDefault(previewId);
            return Task.FromResult(item is not null &&
                                   item.TryCancel(userId, connectionId, nowUtc)
                ? item
                : null);
        }

        public Task<bool> ReplaceAsync(
            McpPreview preview,
            int expectedVersion,
            CancellationToken cancellationToken = default)
        {
            _items[preview.Id] = preview;
            return Task.FromResult(true);
        }
    }

    private sealed class JournalRepositoryFake(List<string> events)
        : IMcpOperationJournalRepository, IMcpConfirmationJournalRepository
    {
        public List<McpOperationJournal> Items { get; } = [];
        public bool FailAdd { get; set; }
        public bool FailReplace { get; set; }

        public Task AddAsync(
            McpOperationJournal journal,
            CancellationToken cancellationToken = default)
        {
            events.Add("journal");
            if (FailAdd)
                throw new InvalidOperationException("journal unavailable");
            Items.Add(journal);
            return Task.CompletedTask;
        }

        public Task CompleteAsync(
            McpOperationJournal journal,
            object? resultSummary,
            CancellationToken cancellationToken = default)
        {
            events.Add("journal-save");
            return Task.CompletedTask;
        }

        public Task FailAsync(
            McpOperationJournal journal,
            string errorCode,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<McpJournalCreateResult> CreateOrGetAsync(
            McpOperationJournal journal,
            CancellationToken cancellationToken = default)
        {
            if (FailAdd)
                throw new InvalidOperationException("journal unavailable");
            var existing = Items.FirstOrDefault(item =>
                (item.PreviewId == journal.PreviewId &&
                 item.PreviewId is not null) ||
                (item.UserId == journal.UserId &&
                 item.ConnectionId == journal.ConnectionId &&
                 item.ToolName == journal.ToolName &&
                 item.IdempotencyKey == journal.IdempotencyKey &&
                 item.IdempotencyKey is not null));
            if (existing is not null)
            {
                return Task.FromResult(new McpJournalCreateResult(
                    existing,
                    false,
                    existing.RequestHash != journal.RequestHash));
            }
            events.Add("confirm-journal");
            Items.Add(journal);
            return Task.FromResult(new McpJournalCreateResult(journal, true, false));
        }

        public Task<McpOperationJournal?> GetOwnedAsync(
            string operationId,
            string userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(item =>
                item.Id == operationId && item.UserId == userId));

        public Task<McpOperationJournal?> GetByPreviewAsync(
            string previewId,
            string userId,
            string connectionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(item =>
                item.PreviewId == previewId &&
                item.UserId == userId &&
                item.ConnectionId == connectionId));

        public Task<McpOperationJournal?> TryAcquireLeaseAsync(
            string operationId,
            string leaseOwner,
            DateTime nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            var item = Items.Single(entry => entry.Id == operationId);
            return Task.FromResult(
                item.TryAcquireLease(leaseOwner, nowUtc, leaseDuration)
                    ? item
                    : null);
        }

        public Task<bool> ReplaceAsync(
            McpOperationJournal journal,
            int expectedVersion,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(!FailReplace);

        public Task<IReadOnlyList<McpOperationJournal>> ListRecoverableAsync(
            DateTime nowUtc,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<McpOperationJournal>>([]);
    }
}
