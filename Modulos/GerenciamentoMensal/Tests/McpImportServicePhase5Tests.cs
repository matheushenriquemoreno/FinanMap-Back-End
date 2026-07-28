#nullable enable

using System.Security.Cryptography;
using System.Text.Json;
using Application.Mcp.Interfaces;
using Application.Mcp.Models;
using Application.Mcp.Services;
using Domain.Mcp.Entities;
using Domain.Mcp.Repositories;
using Xunit;

namespace Tests;

public sealed class McpImportServicePhase5Tests
{
    private static readonly McpCallContext Context =
        new("owner-a", "connection-a", "correlation-a");

    [Fact]
    public async Task MCP_72_73_rejects_more_than_1000_items_before_persistence()
    {
        var repository = new ImportRepositoryFake();
        var service = CreateService(repository);
        var items = Enumerable.Range(1, 1001)
            .Select(index => Expense($"item-{index}", "10.00"))
            .ToArray();

        var result = await service.PrepareAsync(
            Context,
            "request-a",
            items,
            CancellationToken.None);

        Assert.Equal("rejected", result.Status);
        Assert.Contains(result.Errors, error =>
            error.Code == "LIMIT_EXCEEDED" &&
            error.Details!["maximumItems"]!.Equals(1000));
        Assert.Contains(result.Warnings, warning =>
            warning.Contains("lotes menores", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, repository.CreateBatchCalls);
        Assert.Equal(0, repository.AddItemsCalls);
    }

    [Fact]
    public async Task MCP_73_accepts_exactly_1000_structured_items()
    {
        var repository = new ImportRepositoryFake();
        var service = CreateService(repository);
        var items = Enumerable.Range(1, 1000)
            .Select(index => Expense(
                $"item-{index}",
                "10.00",
                $"Despesa {index}"))
            .ToArray();

        var result = await service.PrepareAsync(
            Context,
            "request-1000",
            items,
            CancellationToken.None);

        Assert.Equal("requires_confirmation", result.Status);
        Assert.Equal(1000, result.Data!.Counts.Total);
        Assert.Equal(1000, Assert.Single(repository.AddedItemBatches).Count);
    }

    [Fact]
    public async Task MCP_75_rejects_duplicate_client_item_id_before_any_partial_insert()
    {
        var repository = new ImportRepositoryFake();
        var service = CreateService(repository);

        var result = await service.PrepareAsync(
            Context,
            "request-duplicate-id",
            [
                Expense("same-id", "10.00"),
                Expense("same-id", "20.00")
            ],
            CancellationToken.None);

        Assert.Equal("rejected", result.Status);
        Assert.Contains(result.Errors, error =>
            error.Code == "DUPLICATE_CLIENT_ITEM_ID");
        Assert.Equal(0, repository.CreateBatchCalls);
        Assert.Equal(0, repository.AddItemsCalls);
    }

    [Fact]
    public async Task MCP_72_73_rejects_payload_larger_than_5_mib_before_persistence()
    {
        var repository = new ImportRepositoryFake();
        var service = CreateService(repository);
        var oversized = Expense(
            "item-large",
            "10.00",
            new string('x', (5 * 1024 * 1024) + 1));

        var result = await service.PrepareAsync(
            Context,
            "request-large",
            [oversized],
            CancellationToken.None);

        Assert.Equal("rejected", result.Status);
        Assert.Contains(result.Errors, error =>
            error.Code == "LIMIT_EXCEEDED" &&
            error.Details!["maximumBytes"]!.Equals(5 * 1024 * 1024));
        Assert.Equal(0, repository.CreateBatchCalls);
        Assert.Equal(0, repository.AddItemsCalls);
    }

    [Theory]
    [InlineData("data:application/pdf;base64,JVBERi0xLjQ=")]
    [InlineData("UEsDBBQAAAAIAAAAIQAAAAAAAAAAAAAAAAAJAAAAdGVzdC50eHQ=")]
    public async Task MCP_72_74_rejects_document_or_base64_content_before_persistence(
        string forbiddenContent)
    {
        var repository = new ImportRepositoryFake();
        var service = CreateService(repository);

        var result = await service.PrepareAsync(
            Context,
            "request-document",
            [Expense("item-document", "10.00", forbiddenContent)],
            CancellationToken.None);

        Assert.Equal("rejected", result.Status);
        Assert.Contains(result.Errors, error =>
            error.Code == "UNSUPPORTED_IMPORT_CONTENT");
        Assert.Equal(0, repository.CreateBatchCalls);
        Assert.Equal(0, repository.AddItemsCalls);
    }

    [Fact]
    public async Task MCP_75_80_previews_all_five_types_independently_encrypts_payload_and_aggregates_totals()
    {
        var repository = new ImportRepositoryFake();
        var gateway = new DomainGatewayFake();
        var service = CreateService(repository, gateway);
        var items = new McpImportItemInput[]
        {
            new(
                "category-1",
                McpImportEntityType.Category,
                "Categorias!A2",
                new McpImportItemData(
                    null, null, null, null,
                    Domain.Enum.TipoCategoria.Despesa,
                    "Mercado", null, null, null),
                null,
                null),
            Transaction("income-1", McpImportEntityType.Income, "1000.00"),
            Transaction("expense-1", McpImportEntityType.Expense, "250.50"),
            Transaction("investment-1", McpImportEntityType.Investment, "300.00"),
            new(
                "fixed-1",
                McpImportEntityType.FixedCost,
                null,
                new McpImportItemData(
                    null, null, null, null, null,
                    "Internet", 10, "category-a", true),
                null,
                null)
        };

        var result = await service.PrepareAsync(
            Context,
            "request-five-types",
            items,
            CancellationToken.None);

        Assert.Equal("requires_confirmation", result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(5, result.Data.Counts.Total);
        Assert.Equal(5, result.Data.Counts.Valid);
        Assert.Equal("1000.00", result.Data.Totals.Income);
        Assert.Equal("250.50", result.Data.Totals.Expense);
        Assert.Equal("300.00", result.Data.Totals.Investment);
        Assert.Equal("IMPORT_VALID_ITEMS", result.Data.RequiredDecision);
        Assert.Equal("fixed-1", result.Data.Items.Single(
            item => item.ClientItemId == "fixed-1").SourceRef);
        Assert.Equal(5, Assert.Single(repository.AddedItemBatches).Count);
        Assert.All(Assert.Single(repository.AddedItemBatches), item =>
        {
            Assert.NotEmpty(item.NormalizedDataCiphertext);
            Assert.DoesNotContain(
                item.ClientItemId,
                System.Text.Encoding.UTF8.GetString(item.NormalizedDataCiphertext),
                StringComparison.Ordinal);
        });

        var confirmed = await service.ConfirmAsync(
            Context,
            result.Data.BatchId,
            result.Data.PayloadHash,
            McpImportConfirmationDecision.IMPORT_VALID_ITEMS,
            CancellationToken.None);
        Assert.Equal(5, confirmed.Data!.Counts.Completed);
        Assert.Equal(McpWriteEntity.Category, gateway.ExecutedCommands[0].Entity);
        Assert.Equal(5, gateway.ExecutedCommands.Count);
    }

    [Fact]
    public async Task MCP_78_79_marks_duplicates_explicitly_and_never_silently_discards_them()
    {
        var repository = new ImportRepositoryFake();
        var service = CreateService(repository);
        var original = Expense("expense-a", "50.00", "Táxi");
        var duplicate = Expense("expense-b", "50.00", "Táxi");
        var importAnyway = Expense("expense-c", "50.00", "Táxi") with
        {
            DuplicateDecision = McpImportDuplicateDecision.ImportAnyway
        };
        var skip = Expense("expense-d", "50.00", "Táxi") with
        {
            DuplicateDecision = McpImportDuplicateDecision.Skip
        };

        var result = await service.PrepareAsync(
            Context,
            "request-duplicates",
            [original, duplicate, importAnyway, skip],
            CancellationToken.None);

        Assert.NotNull(result.Data);
        Assert.Equal("valid", result.Data.Items[0].ValidationState);
        Assert.Equal("possible_duplicate", result.Data.Items[1].ValidationState);
        Assert.Equal("valid", result.Data.Items[2].ValidationState);
        Assert.Equal("skipped", result.Data.Items[3].ValidationState);
        Assert.Equal(1, result.Data.Counts.PossibleDuplicate);
        Assert.Equal(1, result.Data.Counts.Skipped);
        Assert.Contains(result.Data.Guidance, item =>
            item.Contains("import_anyway", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MCP_82_84_blocks_owner_scoped_legacy_financial_duplicate_until_explicit_decision()
    {
        var repository = new ImportRepositoryFake();
        var gateway = new DomainGatewayFake
        {
            LegacyDuplicate = true
        };
        var service = CreateService(repository, gateway);

        var blocked = await service.PrepareAsync(
            Context,
            "request-legacy-duplicate",
            [Expense("legacy-duplicate", "50.00", "Táxi")],
            CancellationToken.None);
        var allowed = await service.PrepareAsync(
            Context,
            "request-legacy-duplicate-allowed",
            [
                Expense("legacy-duplicate-allowed", "50.00", "Táxi") with
                {
                    DuplicateDecision = McpImportDuplicateDecision.ImportAnyway
                }
            ],
            CancellationToken.None);

        Assert.Equal(
            "possible_duplicate",
            Assert.Single(blocked.Data!.Items).ValidationState);
        Assert.Equal("valid", Assert.Single(allowed.Data!.Items).ValidationState);
        Assert.All(gateway.DuplicateChecks, check =>
            Assert.Equal("owner-a", check.UserId));
        Assert.All(gateway.DuplicateChecks, check =>
        {
            Assert.Equal(McpWriteEntity.Expense, check.Command.Entity);
            Assert.Equal(2026, check.Command.Values["year"]);
            Assert.Equal(7, check.Command.Values["month"]);
            Assert.Equal("50.00", check.Command.Values["amount"]);
            Assert.Equal("category-a", check.Command.Values["categoryId"]);
            Assert.Equal("Táxi", check.Command.Values["description"]);
        });
    }

    [Fact]
    public async Task MCP_76_77_keeps_category_dependents_pending_until_category_creation()
    {
        var repository = new ImportRepositoryFake();
        var gateway = new DomainGatewayFake();
        var service = CreateService(repository, gateway);
        var category = new McpImportItemInput(
            "new-category",
            McpImportEntityType.Category,
            null,
            new McpImportItemData(
                null, null, null, null,
                Domain.Enum.TipoCategoria.Despesa,
                "Pets", null, null, null),
            null,
            null);
        var dependent = Expense("expense-pet", "80.00", "Ração") with
        {
            Data = Expense("unused", "80.00").Data with { CategoryId = null },
            CategoryHint = "new-category"
        };

        var result = await service.PrepareAsync(
            Context,
            "request-dependent",
            [category, dependent],
            CancellationToken.None);

        Assert.NotNull(result.Data);
        Assert.Equal("valid", result.Data.Items[0].ValidationState);
        Assert.Equal("pending", result.Data.Items[1].ValidationState);
        Assert.Contains(result.Data.Items[1].Suggestions, item =>
            item.Contains("new-category", StringComparison.Ordinal));
        Assert.Equal(1, result.Data.Counts.Pending);

        var confirmed = await service.ConfirmAsync(
            Context,
            result.Data.BatchId,
            result.Data.PayloadHash,
            McpImportConfirmationDecision.IMPORT_VALID_ITEMS,
            CancellationToken.None);
        Assert.Equal("completed", confirmed.Status);
        Assert.Equal(
            "completed",
            confirmed.Data!.Items.Single(item =>
                item.ClientItemId == "expense-pet").ExecutionState);
        Assert.Equal(
            [McpWriteEntity.Category, McpWriteEntity.Expense],
            gateway.ExecutedCommands.Select(command => command.Entity));
    }

    [Fact]
    public async Task MCP_76_77_resolves_only_unique_compatible_category_name_and_suggests_ambiguous_matches()
    {
        var repository = new ImportRepositoryFake();
        var resolver = new CategoryResolverFake
        {
            Matches =
            [
                new McpImportCategoryMatch(
                    "category-a",
                    "Alimentação",
                    Domain.Enum.TipoCategoria.Despesa)
            ]
        };
        var service = CreateService(repository, categories: resolver);
        var unique = Expense("unique-category", "10.00") with
        {
            Data = Expense("unused", "10.00").Data with { CategoryId = null },
            CategoryHint = "Alimentacao"
        };

        var resolved = await service.PrepareAsync(
            Context,
            "request-unique-category",
            [unique],
            CancellationToken.None);
        resolver.Matches =
        [
            new McpImportCategoryMatch(
                "category-a",
                "Casa",
                Domain.Enum.TipoCategoria.Despesa),
            new McpImportCategoryMatch(
                "category-b",
                "CASA",
                Domain.Enum.TipoCategoria.Despesa)
        ];
        var ambiguous = await service.PrepareAsync(
            Context,
            "request-ambiguous-category",
            [unique with { ClientItemId = "ambiguous", CategoryHint = "casa" }],
            CancellationToken.None);

        Assert.Equal("valid", Assert.Single(resolved.Data!.Items).ValidationState);
        Assert.Equal(
            "pending",
            Assert.Single(ambiguous.Data!.Items).ValidationState);
        Assert.Contains(
            Assert.Single(ambiguous.Data.Items).Suggestions,
            suggestion => suggestion.Contains("category-a", StringComparison.Ordinal));
        Assert.Contains(
            Assert.Single(ambiguous.Data.Items).Suggestions,
            suggestion => suggestion.Contains("category-b", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MCP_81_85_confirms_only_valid_items_journal_before_effect_and_does_not_rerun_completed_items()
    {
        var repository = new ImportRepositoryFake();
        var gateway = new DomainGatewayFake();
        var operations = new ConfirmationRepositoryFake();
        gateway.BeforeEffect = () => operations.Events.Add("effect");
        var service = CreateService(repository, gateway, operations);
        var invalid = Expense("invalid", "20.00") with
        {
            Data = Expense("unused", "20.00").Data with { Month = 13 }
        };
        var preview = await service.PrepareAsync(
            Context,
            "request-partial",
            [Expense("valid", "10.00"), invalid],
            CancellationToken.None);

        var first = await service.ConfirmAsync(
            Context,
            preview.Data!.BatchId,
            preview.Data.PayloadHash,
            McpImportConfirmationDecision.IMPORT_VALID_ITEMS,
            CancellationToken.None);
        var replay = await service.ConfirmAsync(
            Context,
            preview.Data.BatchId,
            preview.Data.PayloadHash,
            McpImportConfirmationDecision.IMPORT_VALID_ITEMS,
            CancellationToken.None);

        Assert.Equal("partial", first.Status);
        Assert.Equal(1, first.Data!.Counts.Completed);
        Assert.Equal(1, first.Data.Counts.Invalid);
        Assert.Single(gateway.ExecutedCommands);
        Assert.Equal(1, replay.Data!.Counts.Completed);
        Assert.Single(gateway.ExecutedCommands);
        Assert.Contains("journal-before-effect", operations.Events);
        Assert.True(
            operations.Events.IndexOf("journal-before-effect") <
            operations.Events.IndexOf("effect"));
        Assert.All(
            operations.Items.Where(journal =>
                journal.ToolName == "finanmap_import_confirm"),
            journal =>
            Assert.Equal(Domain.Mcp.Enums.McpOperationClass.Import, journal.OperationClass));
    }

    [Fact]
    public async Task MCP_88_status_is_owner_and_connection_scoped()
    {
        var repository = new ImportRepositoryFake();
        var service = CreateService(repository);
        var preview = await service.PrepareAsync(
            Context,
            "request-status",
            [Expense("item-status", "10.00")],
            CancellationToken.None);

        var denied = await service.GetStatusAsync(
            Context with { UserId = "owner-b" },
            preview.Data!.BatchId,
            CancellationToken.None);
        var allowed = await service.GetStatusAsync(
            Context,
            preview.Data.BatchId,
            CancellationToken.None);

        Assert.Equal("rejected", denied.Status);
        Assert.Contains(denied.Errors, error => error.Code == "NOT_FOUND");
        Assert.Equal("prepared", allowed.Status);
        Assert.Equal("item-status", Assert.Single(allowed.Data!.Items).ClientItemId);
    }

    [Fact]
    public async Task MCP_81_85_unknown_effect_is_not_retried_and_status_reconciles_known_marker()
    {
        var repository = new ImportRepositoryFake();
        var gateway = new DomainGatewayFake
        {
            ExecuteEffect = McpDomainEffect.Unknown("falha injetada")
        };
        var service = CreateService(repository, gateway);
        var preview = await service.PrepareAsync(
            Context,
            "request-unknown",
            [Expense("unknown", "10.00")],
            CancellationToken.None);

        var confirmed = await service.ConfirmAsync(
            Context,
            preview.Data!.BatchId,
            preview.Data.PayloadHash,
            McpImportConfirmationDecision.IMPORT_VALID_ITEMS,
            CancellationToken.None);
        var stillUnknown = await service.ConfirmAsync(
            Context,
            preview.Data.BatchId,
            preview.Data.PayloadHash,
            McpImportConfirmationDecision.IMPORT_VALID_ITEMS,
            CancellationToken.None);
        gateway.FoundEffect = McpDomainEffect.Completed(
            "expense-reconciled",
            "marker-reconciled",
            new Dictionary<string, object?>());
        var reconciled = await service.GetStatusAsync(
            Context,
            preview.Data.BatchId,
            CancellationToken.None);

        Assert.Equal("unknown", Assert.Single(confirmed.Data!.Items).ExecutionState);
        Assert.Equal("unknown", Assert.Single(stillUnknown.Data!.Items).ExecutionState);
        Assert.Single(gateway.ExecutedCommands);
        Assert.Equal("completed", Assert.Single(reconciled.Data!.Items).ExecutionState);
        Assert.Equal("expense-reconciled", Assert.Single(reconciled.Data.Items).EntityId);
    }

    [Fact]
    public async Task MCP_123_124_marks_item_unknown_when_final_journal_cas_fails_after_effect()
    {
        var repository = new ImportRepositoryFake();
        var gateway = new DomainGatewayFake();
        var operations = new ConfirmationRepositoryFake
        {
            FailFinalReplaceOnce = true
        };
        var service = CreateService(
            repository,
            gateway,
            operations);
        var preview = await service.PrepareAsync(
            Context,
            "request-journal-cas",
            [Expense("journal-cas", "10.00")],
            CancellationToken.None);

        var confirmed = await service.ConfirmAsync(
            Context,
            preview.Data!.BatchId,
            preview.Data.PayloadHash,
            McpImportConfirmationDecision.IMPORT_VALID_ITEMS,
            CancellationToken.None);

        Assert.Equal(
            "unknown",
            Assert.Single(confirmed.Data!.Items).ExecutionState);
        Assert.Contains(
            Assert.Single(confirmed.Data.Items).Errors,
            error => error.Code == "JOURNAL_PERSISTENCE_UNKNOWN");
        Assert.NotEmpty(Assert.Single(repository.AddedItemBatches).Single()
            .NormalizedDataCiphertext);

        gateway.FoundEffect = McpDomainEffect.Completed(
            "expense-reconciled-after-journal-cas",
            "marker-reconciled-after-journal-cas",
            new Dictionary<string, object?>());
        var reconciled = await service.GetStatusAsync(
            Context,
            preview.Data.BatchId,
            CancellationToken.None);
        Assert.Equal(
            "completed",
            Assert.Single(reconciled.Data!.Items).ExecutionState);
        Assert.Single(gateway.ExecutedCommands);
    }

    [Fact]
    public async Task MCP_123_124_reconciles_completed_journal_when_item_cas_fails_after_effect()
    {
        var repository = new ImportRepositoryFake
        {
            FailCompletedItemReplaceOnce = true
        };
        var gateway = new DomainGatewayFake();
        var service = CreateService(repository, gateway);
        var preview = await service.PrepareAsync(
            Context,
            "request-item-cas",
            [Expense("item-cas", "10.00")],
            CancellationToken.None);

        var confirmed = await service.ConfirmAsync(
            Context,
            preview.Data!.BatchId,
            preview.Data.PayloadHash,
            McpImportConfirmationDecision.IMPORT_VALID_ITEMS,
            CancellationToken.None);

        Assert.Equal(
            "already_applied",
            Assert.Single(confirmed.Data!.Items).ExecutionState);
        Assert.Single(gateway.ExecutedCommands);
        Assert.True(repository.ReplaceItemCalls >= 2);
    }

    [Fact]
    public async Task MCP_86_87_correction_accepts_only_correctable_items_and_marks_completed_as_already_applied()
    {
        var repository = new ImportRepositoryFake();
        var gateway = new DomainGatewayFake();
        var service = CreateService(repository, gateway);
        var invalid = Expense("invalid-row", "20.00") with
        {
            Data = Expense("unused", "20.00").Data with { Month = 13 }
        };
        var original = await service.PrepareAsync(
            Context,
            "request-original",
            [invalid, Expense("completed-row", "10.00")],
            CancellationToken.None);
        await service.ConfirmAsync(
            Context,
            original.Data!.BatchId,
            original.Data.PayloadHash,
            McpImportConfirmationDecision.IMPORT_VALID_ITEMS,
            CancellationToken.None);

        var correction = await service.PrepareCorrectionAsync(
            Context,
            "request-correction",
            original.Data.BatchId,
            [
                Expense("invalid-row", "20.00"),
                Expense("completed-row", "10.00")
            ],
            CancellationToken.None);
        var corrected = await service.ConfirmAsync(
            Context,
            correction.Data!.BatchId,
            correction.Data.PayloadHash,
            McpImportConfirmationDecision.IMPORT_VALID_ITEMS,
            CancellationToken.None);

        Assert.Equal(original.Data.BatchId, correction.Data.Items.Count > 0
            ? repository.Batch!.ParentBatchId
            : null);
        Assert.Equal(
            "already_applied",
            correction.Data.Items.Single(item =>
                item.ClientItemId == "completed-row").ExecutionState);
        Assert.Equal(
            "completed",
            corrected.Data!.Items.Single(item =>
                item.ClientItemId == "invalid-row").ExecutionState);
        Assert.Equal(2, gateway.ExecutedCommands.Count);
    }

    [Fact]
    public async Task MCP_92_preserves_actionable_domain_failure_message_and_guidance_per_item()
    {
        var repository = new ImportRepositoryFake();
        var gateway = new DomainGatewayFake
        {
            ExecuteEffect = McpDomainEffect.Rejected(
                "CATEGORY_RELATIONSHIP_INVALID",
                "A categoria informada não pode ser usada nesta despesa.")
        };
        var service = CreateService(repository, gateway);
        var preview = await service.PrepareAsync(
            Context,
            "request-domain-failure",
            [Expense("domain-failure", "10.00")],
            CancellationToken.None);

        var result = await service.ConfirmAsync(
            Context,
            preview.Data!.BatchId,
            preview.Data.PayloadHash,
            McpImportConfirmationDecision.IMPORT_VALID_ITEMS,
            CancellationToken.None);

        var item = Assert.Single(result.Data!.Items);
        var error = Assert.Single(item.Errors);
        Assert.Equal("CATEGORY_RELATIONSHIP_INVALID", error.Code);
        Assert.Equal(
            "A categoria informada não pode ser usada nesta despesa.",
            error.Message);
        Assert.Contains(item.Suggestions, guidance =>
            guidance.Contains("categoria", StringComparison.OrdinalIgnoreCase) &&
            guidance.Contains("reenvie", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MCP_98_104_audits_preview_status_correction_and_rejections_with_safe_explicit_origin()
    {
        var repository = new ImportRepositoryFake();
        var operations = new ConfirmationRepositoryFake();
        var service = CreateService(
            repository,
            new DomainGatewayFake(),
            operations);
        var context = Context with
        {
            ProtocolRevision = "2025-11-25",
            ClientId = "client-a"
        };
        var sensitiveDescription = "IMPORT_PAYLOAD_CANARY";

        var rejectedPreview = await service.PrepareAsync(
            context,
            "",
            [Expense("rejected", "10.00", sensitiveDescription)],
            CancellationToken.None);
        var preview = await service.PrepareAsync(
            context,
            "request-audit",
            [Expense("audited", "10.00", sensitiveDescription)],
            CancellationToken.None);
        _ = await service.GetStatusAsync(
            context,
            preview.Data!.BatchId,
            CancellationToken.None);
        _ = await service.PrepareCorrectionAsync(
            context,
            "request-correction-rejected",
            "missing-parent",
            [Expense("missing", "10.00", sensitiveDescription)],
            CancellationToken.None);
        _ = await service.ConfirmAsync(
            context,
            preview.Data.BatchId,
            "wrong-hash",
            McpImportConfirmationDecision.IMPORT_VALID_ITEMS,
            CancellationToken.None);

        Assert.Equal("rejected", rejectedPreview.Status);
        var invocations = operations.Items
            .Where(journal => journal.IdempotencyKey is null)
            .ToArray();
        Assert.Contains(invocations, journal =>
            journal.ToolName == "finanmap_import_preview" &&
            journal.State == Domain.Mcp.Enums.McpOperationState.Rejected);
        Assert.Contains(invocations, journal =>
            journal.ToolName == "finanmap_import_preview" &&
            journal.State == Domain.Mcp.Enums.McpOperationState.Completed);
        Assert.Contains(invocations, journal =>
            journal.ToolName == "finanmap_import_status" &&
            journal.State == Domain.Mcp.Enums.McpOperationState.Completed);
        Assert.Contains(invocations, journal =>
            journal.ToolName == "finanmap_import_correction" &&
            journal.State == Domain.Mcp.Enums.McpOperationState.Rejected);
        Assert.Contains(invocations, journal =>
            journal.ToolName == "finanmap_import_confirm" &&
            journal.State == Domain.Mcp.Enums.McpOperationState.Rejected);
        Assert.All(invocations, journal =>
        {
            Assert.Equal("mcp", journal.Origin["channel"]);
            Assert.Equal("client-a", journal.Origin["clientId"]);
            Assert.Equal("2025-11-25", journal.Origin["protocolRevision"]);
            Assert.Equal("owner-a", journal.UserId);
            Assert.Equal("connection-a", journal.ConnectionId);
        });
        var serialized = JsonSerializer.Serialize(invocations);
        Assert.DoesNotContain(sensitiveDescription, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("wrong-hash", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("\"payload\"", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(invocations, journal =>
            journal.ResultSummary.ContainsKey("importBatch"));
    }

    private static McpImportService CreateService(
        ImportRepositoryFake repository,
        DomainGatewayFake? gateway = null,
        ConfirmationRepositoryFake? operations = null,
        IMcpImportCategoryResolver? categories = null) =>
        new(
            gateway ?? new DomainGatewayFake(),
            new ConnectionValidatorFake(),
            repository,
            operations ?? new ConfirmationRepositoryFake(),
            new McpPreviewPayloadProtector(RandomNumberGenerator.GetBytes(32)),
            TimeProvider.System,
            categories ?? new CategoryResolverFake());

    private static McpImportItemInput Expense(
        string clientItemId,
        string amount,
        string description = "Mercado") =>
        new(
            clientItemId,
            McpImportEntityType.Expense,
            null,
            new McpImportItemData(
                2026,
                7,
                description,
                amount,
                null,
                null,
                null,
                "category-a",
                null),
            null,
            null);

    private static McpImportItemInput Transaction(
        string clientItemId,
        McpImportEntityType type,
        string amount) =>
        new(
            clientItemId,
            type,
            null,
            new McpImportItemData(
                2026,
                7,
                "Descrição",
                amount,
                null,
                null,
                null,
                "category-a",
                null),
            null,
            null);

    private sealed class ConnectionValidatorFake : IMcpConnectionValidator
    {
        public Task<McpConnection> ValidateActiveAsync(
            string connectionId,
            string userId,
            string requiredScope,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal("mcp:import", requiredScope);
            return Task.FromResult<McpConnection>(null!);
        }
    }

    private sealed class CategoryResolverFake : IMcpImportCategoryResolver
    {
        public IReadOnlyList<McpImportCategoryMatch> Matches { get; set; } = [];

        public Task<IReadOnlyList<McpImportCategoryMatch>> FindMatchesAsync(
            string userId,
            string hint,
            Domain.Enum.TipoCategoria expectedType,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Matches);
    }

    private sealed class DomainGatewayFake : IMcpWriteDomainGateway
    {
        public sealed record DuplicateCheck(string UserId, McpWriteCommand Command);

        public List<McpWriteCommand> ExecutedCommands { get; } = [];
        public List<DuplicateCheck> DuplicateChecks { get; } = [];
        public Action? BeforeEffect { get; set; }
        public bool LegacyDuplicate { get; set; }
        public McpDomainEffect ExecuteEffect { get; set; } =
            McpDomainEffect.Completed(
                "entity-a",
                "marker-a",
                new Dictionary<string, object?>());
        public McpDomainEffect? FoundEffect { get; set; }

        public Task<McpDomainPreparation> PrepareAsync(
            string userId,
            McpWriteCommand command,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(McpDomainPreparation.Ready(
                command,
                null,
                command.Values,
                []));

        public Task<McpDomainEffect> ExecuteAsync(
            string userId,
            McpWriteCommand command,
            string operationId,
            IReadOnlyList<McpSnapshotHash> snapshots,
            CancellationToken cancellationToken = default)
        {
            BeforeEffect?.Invoke();
            ExecutedCommands.Add(command);
            return Task.FromResult(ExecuteEffect);
        }

        public Task<McpDomainEffect?> FindEffectAsync(
            string userId,
            McpWriteCommand command,
            string operationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(FoundEffect);

        public Task<bool> HasPossibleDuplicateAsync(
            string userId,
            McpWriteCommand command,
            CancellationToken cancellationToken = default)
        {
            DuplicateChecks.Add(new DuplicateCheck(userId, command));
            return Task.FromResult(LegacyDuplicate);
        }
    }

    private sealed class ImportRepositoryFake : IMcpImportRepository
    {
        public int CreateBatchCalls { get; private set; }
        public int AddItemsCalls { get; private set; }
        public List<IReadOnlyCollection<McpImportItem>> AddedItemBatches { get; } = [];
        public McpImportBatch? Batch { get; private set; }
        public bool FailCompletedItemReplaceOnce { get; set; }
        public int ReplaceItemCalls { get; private set; }
        private readonly List<McpImportItem> _items = [];

        public Task<McpImportBatchCreateResult> CreateOrGetBatchAsync(
            McpImportBatch batch,
            CancellationToken cancellationToken = default)
        {
            CreateBatchCalls++;
            Batch = batch;
            return Task.FromResult(new McpImportBatchCreateResult(batch, true));
        }

        public Task AddItemsAsync(
            IReadOnlyCollection<McpImportItem> items,
            CancellationToken cancellationToken = default)
        {
            AddItemsCalls++;
            AddedItemBatches.Add(items);
            _items.AddRange(items);
            return Task.CompletedTask;
        }

        public Task<McpImportBatch?> GetOwnedBatchAsync(
            string batchId,
            string userId,
            string connectionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Batch is not null &&
                            Batch.Id == batchId &&
                            Batch.UserId == userId &&
                            Batch.ConnectionId == connectionId
                ? Batch
                : null);

        public Task<McpImportItem?> GetOwnedItemAsync(
            string batchId,
            string clientItemId,
            string userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(item =>
                item.BatchId == batchId &&
                item.ClientItemId == clientItemId &&
                item.UserId == userId));

        public Task<IReadOnlyList<McpImportItem>> ListOwnedItemsAsync(
            string batchId,
            string userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<McpImportItem>>(
                _items.Where(item =>
                    item.BatchId == batchId &&
                    item.UserId == userId).ToArray());

        public Task<IReadOnlyList<McpImportItem>> FindOwnedByFingerprintAsync(
            string userId,
            string fingerprint,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<McpImportItem>>([]);

        public Task<bool> ReplaceBatchAsync(
            McpImportBatch batch,
            string userId,
            int expectedVersion,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> ReplaceItemAsync(
            McpImportItem item,
            string userId,
            int expectedVersion,
            CancellationToken cancellationToken = default)
        {
            ReplaceItemCalls++;
            if (FailCompletedItemReplaceOnce &&
                item.ExecutionState ==
                Domain.Mcp.Enums.McpImportExecutionState.Completed)
            {
                FailCompletedItemReplaceOnce = false;
                return Task.FromResult(false);
            }
            return Task.FromResult(true);
        }
    }

    private sealed class ConfirmationRepositoryFake
        : IMcpConfirmationJournalRepository
    {
        public List<McpOperationJournal> Items { get; } = [];
        public List<string> Events { get; } = [];
        public bool FailFinalReplaceOnce { get; set; }
        private bool _finalReplaceFailed;

        public Task<McpJournalCreateResult> CreateOrGetAsync(
            McpOperationJournal journal,
            CancellationToken cancellationToken = default)
        {
            var existing = string.IsNullOrWhiteSpace(journal.IdempotencyKey)
                ? null
                : Items.FirstOrDefault(item =>
                    item.IdempotencyKey == journal.IdempotencyKey &&
                    item.UserId == journal.UserId);
            if (existing is not null)
            {
                return Task.FromResult(new McpJournalCreateResult(
                    existing,
                    false,
                    existing.RequestHash != journal.RequestHash));
            }

            Items.Add(journal);
            return Task.FromResult(new McpJournalCreateResult(journal, true, false));
        }

        public Task<McpOperationJournal?> GetOwnedAsync(
            string operationId,
            string userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_finalReplaceFailed
                ? null
                : Items.FirstOrDefault(item =>
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
            CancellationToken cancellationToken = default)
        {
            if (FailFinalReplaceOnce &&
                !string.IsNullOrWhiteSpace(journal.IdempotencyKey) &&
                journal.State == Domain.Mcp.Enums.McpOperationState.Completed)
            {
                FailFinalReplaceOnce = false;
                _finalReplaceFailed = true;
                return Task.FromResult(false);
            }
            if (journal.Steps.Any(step =>
                    step.State == Domain.Mcp.Enums.McpOperationStepState.Executing))
            {
                Events.Add("journal-before-effect");
            }
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<McpOperationJournal>> ListRecoverableAsync(
            DateTime nowUtc,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<McpOperationJournal>>([]);
    }
}
