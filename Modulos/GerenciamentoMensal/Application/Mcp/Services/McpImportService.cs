#nullable enable

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Mcp.Interfaces;
using Application.Mcp.Models;
using Domain.Mcp.Entities;
using Domain.Mcp.Enums;
using Domain.Mcp.Repositories;

namespace Application.Mcp.Services;

public sealed class McpImportService(
    IMcpWriteDomainGateway domain,
    IMcpConnectionValidator connections,
    IMcpImportRepository imports,
    IMcpConfirmationJournalRepository operations,
    IMcpPreviewPayloadProtector protector,
    TimeProvider time,
    IMcpImportCategoryResolver categories) : IMcpImportService
{
    public const int MaximumItems = 1000;
    public const int MaximumPayloadBytes = 5 * 1024 * 1024;
    private static readonly TimeSpan PayloadRetention = TimeSpan.FromHours(24);
    private static readonly TimeSpan PreviewValidity = TimeSpan.FromMinutes(15);

    private readonly IMcpWriteDomainGateway _domain = domain;
    private readonly IMcpConnectionValidator _connections = connections;
    private readonly IMcpImportRepository _imports = imports;
    private readonly IMcpConfirmationJournalRepository _operations = operations;
    private readonly IMcpPreviewPayloadProtector _protector = protector;
    private readonly TimeProvider _time = time;
    private readonly IMcpImportCategoryResolver _categories = categories;

    public Task<McpToolEnvelope<McpImportPreviewData>> PrepareAsync(
        McpCallContext context,
        string requestId,
        IReadOnlyList<McpImportItemInput> items,
        CancellationToken cancellationToken) =>
        AuditInvocationAsync(
            context,
            "finanmap_import_preview",
            McpOperationClass.Preview,
            new Dictionary<string, object?>
            {
                ["requestIdPresent"] = !string.IsNullOrWhiteSpace(requestId),
                ["itemCount"] = items.Count
            },
            () => PrepareCoreAsync(
                context,
                requestId,
                items,
                cancellationToken),
            cancellationToken);

    private async Task<McpToolEnvelope<McpImportPreviewData>> PrepareCoreAsync(
        McpCallContext context,
        string requestId,
        IReadOnlyList<McpImportItemInput> items,
        CancellationToken cancellationToken)
    {
        var boundaryFailure = ValidateBoundary(context, items);
        if (boundaryFailure is not null)
            return boundaryFailure;

        await _connections.ValidateActiveAsync(
            context.ConnectionId,
            context.UserId,
            "mcp:import",
            cancellationToken);

        if (string.IsNullOrWhiteSpace(requestId))
        {
            return RejectedPreview(
                context,
                "VALIDATION_ERROR",
                "Informe o requestId idempotente da importação.",
                "requestId");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var payloadHash = Fingerprint(JsonSerializer.Serialize(items));
        var batch = McpImportBatch.Create(
            context.UserId,
            context.ConnectionId,
            requestId.Trim(),
            null,
            now);
        var persisted = await _imports.CreateOrGetBatchAsync(
            batch,
            cancellationToken);
        if (!persisted.Created)
        {
            if (!string.Equals(
                    persisted.Batch.PayloadHash,
                    payloadHash,
                    StringComparison.Ordinal))
            {
                return RejectedPreview(
                    context,
                    "IDEMPOTENCY_CONFLICT",
                    "O requestId já foi usado com outro conteúdo.",
                    "requestId");
            }

            var replayItems = await _imports.ListOwnedItemsAsync(
                persisted.Batch.Id,
                context.UserId,
                cancellationToken);
            return PreviewEnvelope(
                context,
                persisted.Batch,
                replayItems,
                "requires_confirmation");
        }

        var categoryProposals = items
            .Where(item => item.Type == McpImportEntityType.Category)
            .Select(item => item.ClientItemId.Trim())
            .Where(item => item.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        var seenFingerprints = new HashSet<string>(StringComparer.Ordinal);
        var persistedItems = new List<McpImportItem>(items.Count);

        foreach (var item in items)
        {
            var validation = await ValidateItemAsync(
                context.UserId,
                batch.Id,
                item,
                categoryProposals,
                seenFingerprints,
                now,
                cancellationToken);
            persistedItems.Add(validation);
        }

        await _imports.AddItemsAsync(persistedItems, cancellationToken);
        var counts = Count(persistedItems);
        var totals = Totals(persistedItems, items);
        var expectedVersion = batch.Version;
        batch.MarkPrepared(counts, totals, payloadHash);
        if (!await _imports.ReplaceBatchAsync(
                batch,
                context.UserId,
                expectedVersion,
                cancellationToken))
        {
            return RejectedPreview(
                context,
                "PERSISTENCE_CONFLICT",
                "O lote mudou durante a preparação; consulte o status.",
                null);
        }

        return PreviewEnvelope(
            context,
            batch,
            persistedItems,
            "requires_confirmation");
    }

    public Task<McpToolEnvelope<McpImportStatusData>> ConfirmAsync(
        McpCallContext context,
        string batchId,
        string payloadHash,
        McpImportConfirmationDecision decision,
        CancellationToken cancellationToken) =>
        AuditInvocationAsync(
            context,
            "finanmap_import_confirm",
            McpOperationClass.Import,
            new Dictionary<string, object?>
            {
                ["batchId"] = batchId,
                ["payloadHashPresent"] = !string.IsNullOrWhiteSpace(payloadHash),
                ["decision"] = decision.ToString()
            },
            () => ConfirmCoreAsync(
                context,
                batchId,
                payloadHash,
                decision,
                cancellationToken),
            cancellationToken);

    private async Task<McpToolEnvelope<McpImportStatusData>> ConfirmCoreAsync(
        McpCallContext context,
        string batchId,
        string payloadHash,
        McpImportConfirmationDecision decision,
        CancellationToken cancellationToken)
    {
        await _connections.ValidateActiveAsync(
            context.ConnectionId,
            context.UserId,
            "mcp:import",
            cancellationToken);
        var batch = await _imports.GetOwnedBatchAsync(
            batchId,
            context.UserId,
            context.ConnectionId,
            cancellationToken);
        if (batch is null)
            return RejectedStatus(context, "NOT_FOUND", "Lote de importação não encontrado.");
        if (!string.Equals(batch.PayloadHash, payloadHash, StringComparison.Ordinal))
            return RejectedStatus(context, "PAYLOAD_HASH_MISMATCH", "O hash não corresponde à prévia.");
        if (decision != McpImportConfirmationDecision.IMPORT_VALID_ITEMS)
            return RejectedStatus(context, "DECISION_MISMATCH", "Use a decisão exata IMPORT_VALID_ITEMS.");

        var existingItems = await _imports.ListOwnedItemsAsync(
            batch.Id,
            context.UserId,
            cancellationToken);
        if (batch.State is McpImportBatchState.Partial or
            McpImportBatchState.Completed or
            McpImportBatchState.Failed)
        {
            return StatusEnvelope(context, batch, existingItems);
        }
        if (batch.State != McpImportBatchState.Prepared)
            return RejectedStatus(context, "BATCH_NOT_READY", "O lote não está pronto para confirmação.");

        var now = _time.GetUtcNow().UtcDateTime;
        if (now >= batch.CreatedAtUtc.Add(PreviewValidity))
            return RejectedStatus(context, "PREVIEW_EXPIRED", "A prévia expirou; prepare uma nova.");

        var batchVersion = batch.Version;
        batch.StartProcessing();
        if (!await _imports.ReplaceBatchAsync(
                batch,
                context.UserId,
                batchVersion,
                cancellationToken))
        {
            return RejectedStatus(context, "PERSISTENCE_CONFLICT", "O lote já está sendo processado.");
        }

        var ordered = existingItems
            .OrderBy(item => item.Type == McpImportItemType.Category ? 0 : 1)
            .ThenBy(item => item.CreatedAtUtc)
            .ToArray();
        foreach (var item in ordered)
        {
            if (item.ValidationState == McpImportValidationState.Pending)
            {
                await TryResolveCategoryDependencyAsync(
                    context,
                    item,
                    ordered,
                    cancellationToken);
            }
            if (item.ValidationState != McpImportValidationState.Valid ||
                item.ExecutionState != McpImportExecutionState.Pending)
            {
                continue;
            }

            await ExecuteItemAsync(
                context,
                batch,
                item,
                cancellationToken);
        }

        var refreshed = await _imports.ListOwnedItemsAsync(
            batch.Id,
            context.UserId,
            cancellationToken);
        var counts = Count(refreshed);
        var finalState = counts["unknown"] > 0 || counts["failed"] > 0 ||
                         counts["invalid"] > 0 || counts["pending"] > 0 ||
                         counts["possible_duplicate"] > 0 ||
                         counts["execution_pending"] > 0
            ? counts["completed"] > 0
                ? McpImportBatchState.Partial
                : McpImportBatchState.Failed
            : McpImportBatchState.Completed;
        batchVersion = batch.Version;
        batch.Finish(finalState, counts, batch.Totals, _time.GetUtcNow().UtcDateTime);
        if (!await _imports.ReplaceBatchAsync(
                batch,
                context.UserId,
                batchVersion,
                cancellationToken))
        {
            return RejectedStatus(
                context,
                "PERSISTENCE_PENDING",
                "Os itens foram processados, mas o resumo final ainda precisa ser reconciliado.");
        }

        return StatusEnvelope(context, batch, refreshed);
    }

    public Task<McpToolEnvelope<McpImportStatusData>> GetStatusAsync(
        McpCallContext context,
        string batchId,
        CancellationToken cancellationToken) =>
        AuditInvocationAsync(
            context,
            "finanmap_import_status",
            McpOperationClass.Import,
            new Dictionary<string, object?>
            {
                ["batchId"] = batchId
            },
            () => GetStatusCoreAsync(
                context,
                batchId,
                cancellationToken),
            cancellationToken);

    private async Task<McpToolEnvelope<McpImportStatusData>> GetStatusCoreAsync(
        McpCallContext context,
        string batchId,
        CancellationToken cancellationToken)
    {
        await _connections.ValidateActiveAsync(
            context.ConnectionId,
            context.UserId,
            "mcp:import",
            cancellationToken);
        var batch = await _imports.GetOwnedBatchAsync(
            batchId,
            context.UserId,
            context.ConnectionId,
            cancellationToken);
        if (batch is null)
            return RejectedStatus(context, "NOT_FOUND", "Lote de importação não encontrado.");
        var items = await _imports.ListOwnedItemsAsync(
            batch.Id,
            context.UserId,
            cancellationToken);
        if (items.Any(item =>
                item.ExecutionState == McpImportExecutionState.Unknown))
        {
            items = await ReconcileUnknownAsync(
                context,
                batch,
                items,
                cancellationToken);
        }
        return StatusEnvelope(context, batch, items);
    }

    public Task<McpToolEnvelope<McpImportPreviewData>> PrepareCorrectionAsync(
        McpCallContext context,
        string requestId,
        string parentBatchId,
        IReadOnlyList<McpImportItemInput> items,
        CancellationToken cancellationToken) =>
        AuditInvocationAsync(
            context,
            "finanmap_import_correction",
            McpOperationClass.Preview,
            new Dictionary<string, object?>
            {
                ["requestIdPresent"] = !string.IsNullOrWhiteSpace(requestId),
                ["parentBatchIdPresent"] = !string.IsNullOrWhiteSpace(parentBatchId),
                ["itemCount"] = items.Count
            },
            () => PrepareCorrectionCoreAsync(
                context,
                requestId,
                parentBatchId,
                items,
                cancellationToken),
            cancellationToken);

    private async Task<McpToolEnvelope<McpImportPreviewData>> PrepareCorrectionCoreAsync(
        McpCallContext context,
        string requestId,
        string parentBatchId,
        IReadOnlyList<McpImportItemInput> items,
        CancellationToken cancellationToken)
    {
        var boundaryFailure = ValidateBoundary(context, items);
        if (boundaryFailure is not null)
            return boundaryFailure;
        await _connections.ValidateActiveAsync(
            context.ConnectionId,
            context.UserId,
            "mcp:import",
            cancellationToken);
        if (string.IsNullOrWhiteSpace(requestId))
            return RejectedPreview(context, "VALIDATION_ERROR", "requestId é obrigatório.", "requestId");

        var parent = await _imports.GetOwnedBatchAsync(
            parentBatchId,
            context.UserId,
            context.ConnectionId,
            cancellationToken);
        if (parent is null)
            return RejectedPreview(context, "NOT_FOUND", "Lote pai não encontrado.", "parentBatchId");
        var parentItems = await _imports.ListOwnedItemsAsync(
            parent.Id,
            context.UserId,
            cancellationToken);
        var parentByClientId = parentItems.ToDictionary(
            item => item.ClientItemId,
            StringComparer.Ordinal);
        if (items.Any(item =>
                !parentByClientId.ContainsKey(item.ClientItemId)))
        {
            return RejectedPreview(
                context,
                "CORRECTION_ITEM_NOT_FOUND",
                "Todo clientItemId corrigido deve existir no lote pai.",
                "items");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var payloadHash = Fingerprint(JsonSerializer.Serialize(items));
        var batch = McpImportBatch.Create(
            context.UserId,
            context.ConnectionId,
            requestId.Trim(),
            parent.Id,
            now);
        var persisted = await _imports.CreateOrGetBatchAsync(batch, cancellationToken);
        if (!persisted.Created)
        {
            if (!string.Equals(persisted.Batch.PayloadHash, payloadHash, StringComparison.Ordinal))
                return RejectedPreview(context, "IDEMPOTENCY_CONFLICT", "requestId usado com outro conteúdo.", "requestId");
            var replay = await _imports.ListOwnedItemsAsync(
                persisted.Batch.Id,
                context.UserId,
                cancellationToken);
            return PreviewEnvelope(context, persisted.Batch, replay, "requires_confirmation");
        }

        var proposals = items
            .Where(item => item.Type == McpImportEntityType.Category)
            .Select(item => item.ClientItemId)
            .ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var corrected = new List<McpImportItem>(items.Count);
        foreach (var input in items)
        {
            var previous = parentByClientId[input.ClientItemId];
            var candidate = await ValidateItemAsync(
                context.UserId,
                batch.Id,
                input,
                proposals,
                seen,
                now,
                cancellationToken);
            if (previous.ExecutionState is
                McpImportExecutionState.Completed or
                McpImportExecutionState.AlreadyApplied)
            {
                candidate.MarkAlreadyApplied(
                    previous.OperationId ?? $"already:{parent.Id}:{input.ClientItemId}",
                    now);
            }
            else if (previous.ValidationState is not (
                         McpImportValidationState.Invalid or
                         McpImportValidationState.Pending) &&
                     previous.ExecutionState != McpImportExecutionState.Failed)
            {
                return RejectedPreview(
                    context,
                    "CORRECTION_NOT_ALLOWED",
                    "Somente itens invalid, pending ou failed podem ser corrigidos; completed retorna already_applied.",
                    "items");
            }
            corrected.Add(candidate);
        }

        await _imports.AddItemsAsync(corrected, cancellationToken);
        var counts = Count(corrected);
        var totals = Totals(corrected, items);
        var version = batch.Version;
        batch.MarkPrepared(counts, totals, payloadHash);
        if (!await _imports.ReplaceBatchAsync(
                batch,
                context.UserId,
                version,
                cancellationToken))
        {
            return RejectedPreview(context, "PERSISTENCE_CONFLICT", "O lote de correção mudou.", null);
        }
        return PreviewEnvelope(context, batch, corrected, "requires_confirmation");
    }

    private static McpToolEnvelope<McpImportPreviewData>? ValidateBoundary(
        McpCallContext context,
        IReadOnlyList<McpImportItemInput>? items)
    {
        if (items is null || items.Count == 0)
        {
            return RejectedPreview(
                context,
                "VALIDATION_ERROR",
                "Informe ao menos um item estruturado para importar.",
                "items");
        }

        if (items.Count > MaximumItems)
        {
            return RejectedPreview(
                context,
                "LIMIT_EXCEEDED",
                $"O lote excede o limite de {MaximumItems} itens. Divida-o em lotes menores.",
                "items",
                new Dictionary<string, object?>
                {
                    ["maximumItems"] = MaximumItems,
                    ["receivedItems"] = items.Count
                },
                ["Divida os dados em lotes menores de até 1000 itens."]);
        }

        if (items.Any(item => string.IsNullOrWhiteSpace(item.ClientItemId)))
        {
            return RejectedPreview(
                context,
                "VALIDATION_ERROR",
                "Todo item deve informar um clientItemId não vazio.",
                "items");
        }

        var duplicateClientItemId = items
            .GroupBy(
                item => item.ClientItemId.Trim(),
                StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;
        if (duplicateClientItemId is not null)
        {
            return RejectedPreview(
                context,
                "DUPLICATE_CLIENT_ITEM_ID",
                "clientItemId deve ser único dentro do lote para evitar persistência parcial.",
                "items",
                new Dictionary<string, object?>
                {
                    ["clientItemId"] = duplicateClientItemId
                });
        }

        var serialized = JsonSerializer.SerializeToUtf8Bytes(items);
        if (serialized.Length > MaximumPayloadBytes)
        {
            return RejectedPreview(
                context,
                "LIMIT_EXCEEDED",
                "O lote excede o limite de 5 MiB. Divida-o em lotes menores.",
                "items",
                new Dictionary<string, object?>
                {
                    ["maximumBytes"] = MaximumPayloadBytes,
                    ["receivedBytes"] = serialized.Length
                },
                ["Divida os dados em lotes menores com até 5 MiB cada."]);
        }

        if (items.Any(ContainsForbiddenContent))
        {
            return RejectedPreview(
                context,
                "UNSUPPORTED_IMPORT_CONTENT",
                "A importação aceita somente JSON estruturado; documentos, binários e base64 não são aceitos.",
                "items",
                null,
                ["Extraia somente os campos estruturados e envie-os em JSON."]);
        }

        return null;
    }

    private static bool ContainsForbiddenContent(McpImportItemInput item) =>
        new[]
        {
            item.ClientItemId,
            item.SourceRef,
            item.CategoryHint,
            item.Data.Description,
            item.Data.Amount,
            item.Data.Name,
            item.Data.CategoryId
        }.Any(IsForbiddenString);

    private static bool IsForbiddenString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var candidate = value.Trim();
        if (candidate.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            candidate.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (candidate.StartsWith("JVBERi0", StringComparison.Ordinal) ||
               candidate.StartsWith("UEsDB", StringComparison.Ordinal) ||
               candidate.StartsWith("/9j/", StringComparison.Ordinal) ||
               candidate.StartsWith("iVBOR", StringComparison.Ordinal) ||
               candidate.StartsWith("R0lGOD", StringComparison.Ordinal))
        {
            return true;
        }

        if (candidate.Length < 24 || candidate.Length % 4 != 0)
            return false;
        Span<byte> decoded = candidate.Length <= 4096
            ? stackalloc byte[candidate.Length]
            : new byte[candidate.Length];
        return Convert.TryFromBase64String(
                   candidate,
                   decoded,
                   out var bytesWritten) &&
               bytesWritten >= 16;
    }

    private async Task<McpImportItem> ValidateItemAsync(
        string userId,
        string batchId,
        McpImportItemInput input,
        IReadOnlySet<string> categoryProposals,
        ISet<string> seenFingerprints,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var errors = new List<McpImportItemError>();
        var effectiveInput = input;
        if (string.IsNullOrWhiteSpace(input.ClientItemId))
        {
            errors.Add(Error(
                "VALIDATION_ERROR",
                "clientItemId é obrigatório.",
                "clientItemId"));
        }

        McpImportValidationState? categoryState = null;
        if (
            input.Type != McpImportEntityType.Category &&
            string.IsNullOrWhiteSpace(input.Data.CategoryId))
        {
            if (!string.IsNullOrWhiteSpace(input.CategoryHint) &&
                categoryProposals.Contains(input.CategoryHint.Trim()))
            {
                categoryState = McpImportValidationState.Pending;
                errors.Add(new McpImportItemError(
                    "CATEGORY_DEPENDENCY_PENDING",
                    "O item depende da categoria proposta no mesmo lote.",
                    "categoryHint",
                    $"Crie primeiro a categoria {input.CategoryHint.Trim()}."));
            }
            else if (!string.IsNullOrWhiteSpace(input.CategoryHint))
            {
                var matches = await _categories.FindMatchesAsync(
                    userId,
                    input.CategoryHint.Trim(),
                    ExpectedCategoryType(input.Type),
                    cancellationToken);
                if (matches.Count == 1)
                {
                    effectiveInput = input with
                    {
                        Data = input.Data with { CategoryId = matches[0].Id }
                    };
                }
                else if (matches.Count > 1)
                {
                    categoryState = McpImportValidationState.Pending;
                    errors.AddRange(matches.Select(match =>
                        new McpImportItemError(
                            "CATEGORY_AMBIGUOUS",
                            "Mais de uma categoria própria corresponde ao nome informado.",
                            "categoryHint",
                            $"Selecione explicitamente {match.Id} ({match.Name}).")));
                }
                else
                {
                    categoryState = McpImportValidationState.Pending;
                    errors.Add(new McpImportItemError(
                        "CATEGORY_CREATE_PROPOSAL_REQUIRED",
                        "A categoria não foi encontrada.",
                        "categoryHint",
                        "Inclua uma proposta de categoria no lote ou informe categoryId próprio."));
                }
            }
            else
            {
                categoryState = McpImportValidationState.Pending;
                errors.Add(new McpImportItemError(
                    "CATEGORY_CREATE_PROPOSAL_REQUIRED",
                    "A categoria não foi resolvida.",
                    "categoryHint",
                    "Inclua uma proposta de categoria no lote ou informe categoryId próprio."));
            }
        }

        var validationErrorsBeforeCategory = errors.Count(error =>
            error.Code == "VALIDATION_ERROR");
        var command = BuildCommand(effectiveInput, errors);
        var state = errors.Count(error => error.Code == "VALIDATION_ERROR") >
                    validationErrorsBeforeCategory ||
                    validationErrorsBeforeCategory > 0
            ? McpImportValidationState.Invalid
            : categoryState ?? McpImportValidationState.Valid;

        if (state == McpImportValidationState.Valid && command is not null)
        {
            var preparation = await _domain.PrepareAsync(
                userId,
                command,
                cancellationToken);
            if (!preparation.IsReady)
            {
                state = McpImportValidationState.Invalid;
                errors.AddRange(preparation.Errors.Select(error =>
                    new McpImportItemError(
                        error.Code,
                        error.Message,
                        error.Field,
                        null)));
            }
        }

        var fingerprint = ImportFingerprint(userId, effectiveInput);
        if (state == McpImportValidationState.Valid)
        {
            var persistedDuplicates =
                await _imports.FindOwnedByFingerprintAsync(
                    userId,
                    fingerprint,
                    1,
                    cancellationToken);
            var legacyDuplicate = command is not null &&
                                  await _domain.HasPossibleDuplicateAsync(
                                      userId,
                                      command,
                                      cancellationToken);
            var duplicate = !seenFingerprints.Add(fingerprint) ||
                            legacyDuplicate ||
                            persistedDuplicates.Any(item =>
                                item.ExecutionState is
                                    McpImportExecutionState.Completed or
                                    McpImportExecutionState.AlreadyApplied);
            if (duplicate)
            {
                state = input.DuplicateDecision switch
                {
                    Application.Mcp.Models.McpImportDuplicateDecision.Skip =>
                        McpImportValidationState.Skipped,
                    Application.Mcp.Models.McpImportDuplicateDecision.ImportAnyway =>
                        McpImportValidationState.Valid,
                    _ => McpImportValidationState.PossibleDuplicate
                };
                if (state == McpImportValidationState.PossibleDuplicate)
                {
                    errors.Add(new McpImportItemError(
                        "POSSIBLE_DUPLICATE",
                        "O item pode duplicar um registro do mesmo proprietário.",
                        "duplicateDecision",
                        "Escolha explicitamente skip ou import_anyway."));
                }
            }
        }

        var serialized = JsonSerializer.SerializeToUtf8Bytes(effectiveInput);
        var sourceRef = string.IsNullOrWhiteSpace(input.SourceRef)
            ? input.ClientItemId
            : input.SourceRef.Trim();
        return McpImportItem.Create(
            batchId,
            userId,
            input.ClientItemId.Trim(),
            new McpImportSourceRef(null, null, sourceRef),
            ToDomainType(input.Type),
            _protector.Protect(serialized),
            fingerprint,
            state,
            errors,
            now.Add(PayloadRetention),
            now,
            input.DuplicateDecision switch
            {
                Application.Mcp.Models.McpImportDuplicateDecision.Skip =>
                    Domain.Mcp.Enums.McpImportDuplicateDecision.Skip,
                Application.Mcp.Models.McpImportDuplicateDecision.ImportAnyway =>
                    Domain.Mcp.Enums.McpImportDuplicateDecision.ImportAnyway,
                _ => null
            });
    }

    private static McpWriteCommand? BuildCommand(
        McpImportItemInput input,
        ICollection<McpImportItemError> errors)
    {
        var data = input.Data;
        switch (input.Type)
        {
            case McpImportEntityType.Category:
                Required(data.Name, "name", errors);
                if (data.CategoryType is null)
                    errors.Add(Error("VALIDATION_ERROR", "categoryType é obrigatório.", "categoryType"));
                return errors.Count > 0
                    ? null
                    : new McpWriteCommand(
                        McpWriteEntity.Category,
                        McpPreviewAction.Create,
                        null,
                        new Dictionary<string, object?>
                        {
                            ["name"] = data.Name!.Trim(),
                            ["type"] = data.CategoryType!.Value
                        });

            case McpImportEntityType.FixedCost:
                Required(data.Name, "name", errors);
                if (data.DueDay is null or < 1 or > 31)
                    errors.Add(Error("VALIDATION_ERROR", "dueDay deve estar entre 1 e 31.", "dueDay"));
                return errors.Count > 0
                    ? null
                    : new McpWriteCommand(
                        McpWriteEntity.FixedCost,
                        McpPreviewAction.Create,
                        null,
                        new Dictionary<string, object?>
                        {
                            ["name"] = data.Name!.Trim(),
                            ["dueDay"] = data.DueDay,
                            ["categoryId"] = data.CategoryId,
                            ["active"] = data.Active ?? true
                        });

            case McpImportEntityType.Income:
            case McpImportEntityType.Expense:
            case McpImportEntityType.Investment:
                if (data.Year is null or < 2000 or > 2100)
                    errors.Add(Error("VALIDATION_ERROR", "year está fora do intervalo aceito.", "year"));
                if (data.Month is null or < 1 or > 12)
                    errors.Add(Error("VALIDATION_ERROR", "month deve estar entre 1 e 12.", "month"));
                Required(data.Description, "description", errors);
                if (!TryAmount(data.Amount, out var amount))
                    errors.Add(Error("VALIDATION_ERROR", "amount deve ser positivo com até duas casas decimais.", "amount"));
                return errors.Count > 0
                    ? null
                    : new McpWriteCommand(
                        input.Type switch
                        {
                            McpImportEntityType.Income => McpWriteEntity.Income,
                            McpImportEntityType.Expense => McpWriteEntity.Expense,
                            _ => McpWriteEntity.Investment
                        },
                        McpPreviewAction.Create,
                        null,
                        new Dictionary<string, object?>
                        {
                            ["year"] = data.Year,
                            ["month"] = data.Month,
                            ["description"] = data.Description!.Trim(),
                            ["amount"] = amount.ToString("0.00", CultureInfo.InvariantCulture),
                            ["categoryId"] = data.CategoryId
                        });
            default:
                errors.Add(Error("VALIDATION_ERROR", "type não é suportado.", "type"));
                return null;
        }
    }

    private static void Required(
        string? value,
        string field,
        ICollection<McpImportItemError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors.Add(Error("VALIDATION_ERROR", $"{field} é obrigatório.", field));
    }

    private static McpImportItemError Error(
        string code,
        string message,
        string field) =>
        new(code, message, field, null);

    private static bool TryAmount(string? value, out decimal amount) =>
        decimal.TryParse(
            value,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out amount) &&
        amount > 0 &&
        decimal.Round(amount, 2) == amount;

    private static string ImportFingerprint(
        string userId,
        McpImportItemInput input)
    {
        TryAmount(input.Data.Amount, out var amount);
        var raw = string.Join(
            "|",
            userId,
            input.Type,
            input.Data.Year,
            input.Data.Month,
            amount.ToString("0.00", CultureInfo.InvariantCulture),
            input.Data.CategoryId?.Trim().ToUpperInvariant(),
            input.CategoryHint?.Trim().ToUpperInvariant(),
            input.Data.Description?.Trim().ToUpperInvariant(),
            input.Data.Name?.Trim().ToUpperInvariant(),
            input.Data.CategoryType,
            input.Data.DueDay);
        return Fingerprint(raw);
    }

    private static string Fingerprint(string raw) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(raw)))
        .ToLowerInvariant();

    private static McpImportItemType ToDomainType(McpImportEntityType type) =>
        type switch
        {
            McpImportEntityType.Category => McpImportItemType.Category,
            McpImportEntityType.Income => McpImportItemType.Income,
            McpImportEntityType.Expense => McpImportItemType.Expense,
            McpImportEntityType.Investment => McpImportItemType.Investment,
            McpImportEntityType.FixedCost => McpImportItemType.FixedCost,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

    private static Domain.Enum.TipoCategoria ExpectedCategoryType(
        McpImportEntityType type) =>
        type switch
        {
            McpImportEntityType.Income => Domain.Enum.TipoCategoria.Rendimento,
            McpImportEntityType.Investment => Domain.Enum.TipoCategoria.Investimento,
            McpImportEntityType.Expense or McpImportEntityType.FixedCost =>
                Domain.Enum.TipoCategoria.Despesa,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

    private static Dictionary<string, int> Count(
        IReadOnlyCollection<McpImportItem> items) =>
        new(StringComparer.Ordinal)
        {
            ["total"] = items.Count,
            ["valid"] = items.Count(item => item.ValidationState == McpImportValidationState.Valid),
            ["invalid"] = items.Count(item => item.ValidationState == McpImportValidationState.Invalid),
            ["pending"] = items.Count(item =>
                item.ValidationState == McpImportValidationState.Pending),
            ["execution_pending"] = items.Count(item =>
                item.ValidationState == McpImportValidationState.Valid &&
                item.ExecutionState == McpImportExecutionState.Pending),
            ["possible_duplicate"] = items.Count(item => item.ValidationState == McpImportValidationState.PossibleDuplicate),
            ["skipped"] = items.Count(item => item.ValidationState == McpImportValidationState.Skipped),
            ["completed"] = items.Count(item =>
                item.ExecutionState is
                    McpImportExecutionState.Completed or
                    McpImportExecutionState.AlreadyApplied),
            ["failed"] = items.Count(item => item.ExecutionState == McpImportExecutionState.Failed),
            ["unknown"] = items.Count(item => item.ExecutionState == McpImportExecutionState.Unknown)
        };

    private static Dictionary<string, decimal> Totals(
        IReadOnlyList<McpImportItem> persisted,
        IReadOnlyList<McpImportItemInput> inputs)
    {
        var totals = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["income"] = 0,
            ["expense"] = 0,
            ["investment"] = 0,
            ["fixed_cost"] = 0
        };
        for (var index = 0; index < persisted.Count; index++)
        {
            if (persisted[index].ValidationState != McpImportValidationState.Valid ||
                !TryAmount(inputs[index].Data.Amount, out var amount))
            {
                continue;
            }

            var key = inputs[index].Type switch
            {
                McpImportEntityType.Income => "income",
                McpImportEntityType.Expense => "expense",
                McpImportEntityType.Investment => "investment",
                McpImportEntityType.FixedCost => "fixed_cost",
                _ => null
            };
            if (key is not null)
                totals[key] += amount;
        }
        return totals;
    }

    private static McpToolEnvelope<McpImportPreviewData> PreviewEnvelope(
        McpCallContext context,
        McpImportBatch batch,
        IReadOnlyList<McpImportItem> items,
        string status)
    {
        var counts = MapCounts(batch.Counts);
        var data = new McpImportPreviewData(
            batch.Id,
            StateWire(batch.State),
            counts,
            MapTotals(batch.Totals),
            items.Select(MapItem).ToArray(),
            "IMPORT_VALID_ITEMS",
            batch.CreatedAtUtc.Add(PreviewValidity),
            batch.PayloadHash,
            counts.PossibleDuplicate > 0
                ? ["Para possíveis duplicidades, escolha explicitamente skip ou import_anyway."]
                : []);
        return new McpToolEnvelope<McpImportPreviewData>(
            "1.0",
            context.CorrelationId,
            status,
            data,
            null,
            "BRL",
            new Dictionary<string, object?>
            {
                ["itemCount"] = counts.Total
            },
            null,
            [],
            []);
    }

    private static McpImportCounts MapCounts(
        IReadOnlyDictionary<string, int> counts) =>
        new(
            Value(counts, "total"),
            Value(counts, "valid"),
            Value(counts, "invalid"),
            Value(counts, "pending"),
            Value(counts, "possible_duplicate"),
            Value(counts, "skipped"),
            Value(counts, "completed"),
            Value(counts, "failed"),
            Value(counts, "unknown"));

    private static McpImportTotals MapTotals(
        IReadOnlyDictionary<string, decimal> totals) =>
        new(
            Money(totals, "income"),
            Money(totals, "expense"),
            Money(totals, "investment"),
            Money(totals, "fixed_cost"));

    private static McpImportItemResult MapItem(McpImportItem item) =>
        new(
            item.ClientItemId,
            item.SourceRef?.Item ?? item.ClientItemId,
            TypeWire(item.Type),
            StateWire(item.ValidationState),
            StateWire(item.ExecutionState),
            item.CreatedEntityId,
            item.OperationId,
            item.ErrorDetails.Select(error =>
                new McpToolError(
                    error.Code,
                    error.Message,
                    error.Field,
                    false)).ToArray(),
            item.ErrorDetails
                .Where(error => !string.IsNullOrWhiteSpace(error.Guidance))
                .Select(error => error.Guidance!)
                .ToArray());

    private static int Value(IReadOnlyDictionary<string, int> values, string key) =>
        values.TryGetValue(key, out var value) ? value : 0;

    private static string Money(
        IReadOnlyDictionary<string, decimal> values,
        string key) =>
        (values.TryGetValue(key, out var value) ? value : 0)
        .ToString("0.00", CultureInfo.InvariantCulture);

    private static string StateWire(Enum value) =>
        string.Concat(value.ToString().Select((character, index) =>
            char.IsUpper(character) && index > 0
                ? $"_{char.ToLowerInvariant(character)}"
                : char.ToLowerInvariant(character).ToString()));

    private static string TypeWire(McpImportItemType type) =>
        StateWire(type);

    private async Task ExecuteItemAsync(
        McpCallContext context,
        McpImportBatch batch,
        McpImportItem item,
        CancellationToken cancellationToken)
    {
        McpImportItemInput input;
        try
        {
            input = JsonSerializer.Deserialize<McpImportItemInput>(
                    _protector.Unprotect(item.NormalizedDataCiphertext))
                ?? throw new JsonException("Item protegido vazio.");
        }
        catch
        {
            var version = item.Version;
            item.RecordFailure(
                $"import:{batch.Id}:{item.ClientItemId}",
                "PROTECTED_PAYLOAD_INVALID",
                _time.GetUtcNow().UtcDateTime,
                unknown: true);
            await _imports.ReplaceItemAsync(
                item,
                context.UserId,
                version,
                cancellationToken);
            return;
        }

        var errors = new List<McpImportItemError>();
        var command = BuildCommand(input, errors);
        if (command is null)
            return;

        var now = _time.GetUtcNow().UtcDateTime;
        var protectedPayload = item.NormalizedDataCiphertext.ToArray();
        var itemKey = $"{batch.Id}:{item.ClientItemId}";
        var journal = McpOperationJournal.Start(
            context.UserId,
            context.ConnectionId,
            context.CorrelationId,
            "finanmap_import_confirm",
            McpOperationClass.Import,
            new Dictionary<string, object?>
            {
                ["itemCount"] = 1,
                ["entityType"] = TypeWire(item.Type)
            },
            Origin(context),
            idempotencyKey: itemKey,
            requestHash: Fingerprint($"{batch.PayloadHash}|{item.ClientItemId}"),
            previewId: itemKey,
            steps:
            [
                new McpOperationStep(
                    "effect",
                    McpOperationStepState.Pending,
                    null,
                    null,
                    null)
            ],
            startedAtUtc: now);
        var created = await _operations.CreateOrGetAsync(journal, cancellationToken);
        if (created.RequestConflict)
        {
            var version = item.Version;
            item.RecordFailure(journal.Id, "IDEMPOTENCY_CONFLICT", now);
            await _imports.ReplaceItemAsync(
                item,
                context.UserId,
                version,
                cancellationToken);
            return;
        }
        journal = created.Journal;
        if (!created.Created &&
            journal.State == McpOperationState.Completed)
        {
            if (item.ExecutionState != McpImportExecutionState.Completed)
            {
                var version = item.Version;
                item.MarkAlreadyApplied(journal.Id, now);
                await _imports.ReplaceItemAsync(
                    item,
                    context.UserId,
                    version,
                    cancellationToken);
            }
            return;
        }

        var leaseOwner = $"import:{context.CorrelationId}:{item.ClientItemId}";
        var leased = await _operations.TryAcquireLeaseAsync(
            journal.Id,
            leaseOwner,
            now,
            TimeSpan.FromMinutes(1),
            cancellationToken);
        if (leased is null)
            return;

        var journalVersion = leased.Version;
        leased.StartStep("effect", leaseOwner, now);
        if (!await _operations.ReplaceAsync(
                leased,
                journalVersion,
                cancellationToken))
        {
            return;
        }

        McpDomainEffect effect;
        try
        {
            effect = await _domain.ExecuteAsync(
                context.UserId,
                command,
                journal.Id,
                [],
                cancellationToken);
        }
        catch
        {
            effect = McpDomainEffect.Unknown(
                "O resultado do efeito não pôde ser comprovado.");
        }

        var itemVersion = item.Version;
        journalVersion = leased.Version;
        switch (effect.State)
        {
            case McpDomainEffectState.Completed:
                leased.CompleteStep(
                    "effect",
                    effect.EffectMarker ?? journal.Id,
                    effect.Result,
                    _time.GetUtcNow().UtcDateTime);
                leased.CompleteAt(
                    new Dictionary<string, object?>
                    {
                        ["status"] = "completed",
                        ["entityType"] = TypeWire(item.Type),
                        ["entityIdPresent"] = !string.IsNullOrWhiteSpace(effect.EntityId)
                    },
                    _time.GetUtcNow().UtcDateTime);
                item.RecordResult(
                    journal.Id,
                    effect.EntityId,
                    _time.GetUtcNow().UtcDateTime);
                break;
            case McpDomainEffectState.Unknown:
                leased.FailStep(
                    "effect",
                    "EFFECT_OUTCOME_UNKNOWN",
                    true,
                    _time.GetUtcNow().UtcDateTime);
                leased.MarkUnknown(
                    "EFFECT_OUTCOME_UNKNOWN",
                    _time.GetUtcNow().UtcDateTime);
                item.RecordFailure(
                    journal.Id,
                    "EFFECT_OUTCOME_UNKNOWN",
                    _time.GetUtcNow().UtcDateTime,
                    unknown: true,
                    message: effect.Message,
                    guidance:
                    "Consulte o status do lote antes de tentar novamente; não reenvie este item enquanto o resultado estiver desconhecido.");
                break;
            default:
                var code = effect.ErrorCode ?? "DOMAIN_REJECTED";
                leased.FailStep(
                    "effect",
                    code,
                    false,
                    _time.GetUtcNow().UtcDateTime);
                leased.FailAt(code, _time.GetUtcNow().UtcDateTime);
                item.RecordFailure(
                    journal.Id,
                    code,
                    _time.GetUtcNow().UtcDateTime,
                    message: effect.Message,
                    guidance: DomainFailureGuidance(code));
                break;
        }

        if (!await _operations.ReplaceAsync(
                leased,
                journalVersion,
                cancellationToken))
        {
            var persistedJournal = await _operations.GetOwnedAsync(
                leased.Id,
                context.UserId,
                cancellationToken);
            if (persistedJournal?.State == McpOperationState.Completed)
            {
                item.MarkAlreadyApplied(
                    persistedJournal.Id,
                    _time.GetUtcNow().UtcDateTime);
            }
            else
            {
                item.RecordFailure(
                    leased.Id,
                    "JOURNAL_PERSISTENCE_UNKNOWN",
                    _time.GetUtcNow().UtcDateTime,
                    unknown: true,
                    protectedPayload: protectedPayload);
            }
            if (!await _imports.ReplaceItemAsync(
                    item,
                    context.UserId,
                    itemVersion,
                    cancellationToken))
            {
                await PersistReconciledItemAsync(
                    context,
                    item,
                    leased.Id,
                    cancellationToken);
            }
            return;
        }

        if (!await _imports.ReplaceItemAsync(
                item,
                context.UserId,
                itemVersion,
                cancellationToken))
        {
            item.MarkAlreadyApplied(
                leased.Id,
                _time.GetUtcNow().UtcDateTime);
            if (!await _imports.ReplaceItemAsync(
                    item,
                    context.UserId,
                    itemVersion,
                    cancellationToken))
            {
                await PersistReconciledItemAsync(
                    context,
                    item,
                    leased.Id,
                    cancellationToken);
            }
        }
    }

    private async Task PersistReconciledItemAsync(
        McpCallContext context,
        McpImportItem attempted,
        string operationId,
        CancellationToken cancellationToken)
    {
        var persisted = await _imports.GetOwnedItemAsync(
            attempted.BatchId,
            attempted.ClientItemId,
            context.UserId,
            cancellationToken);
        if (persisted is null ||
            persisted.ExecutionState is McpImportExecutionState.Completed or
                McpImportExecutionState.AlreadyApplied or
                McpImportExecutionState.Unknown)
        {
            return;
        }

        var expectedVersion = persisted.Version;
        if (attempted.ExecutionState == McpImportExecutionState.Unknown)
        {
            persisted.RecordFailure(
                operationId,
                "JOURNAL_PERSISTENCE_UNKNOWN",
                _time.GetUtcNow().UtcDateTime,
                unknown: true,
                protectedPayload: attempted.NormalizedDataCiphertext);
        }
        else
        {
            persisted.MarkAlreadyApplied(
                operationId,
                _time.GetUtcNow().UtcDateTime);
        }
        if (!await _imports.ReplaceItemAsync(
                persisted,
                context.UserId,
                expectedVersion,
                cancellationToken))
        {
            return;
        }
    }

    private async Task TryResolveCategoryDependencyAsync(
        McpCallContext context,
        McpImportItem item,
        IReadOnlyList<McpImportItem> batchItems,
        CancellationToken cancellationToken)
    {
        if (!item.ErrorDetails.Any(error =>
                error.Code == "CATEGORY_DEPENDENCY_PENDING"))
        {
            return;
        }

        McpImportItemInput? input;
        try
        {
            input = JsonSerializer.Deserialize<McpImportItemInput>(
                _protector.Unprotect(item.NormalizedDataCiphertext));
        }
        catch
        {
            return;
        }
        if (input is null || string.IsNullOrWhiteSpace(input.CategoryHint))
            return;
        var category = batchItems.FirstOrDefault(candidate =>
            candidate.Type == McpImportItemType.Category &&
            candidate.ClientItemId == input.CategoryHint &&
            candidate.ExecutionState == McpImportExecutionState.Completed &&
            !string.IsNullOrWhiteSpace(candidate.CreatedEntityId));
        if (category is null)
            return;

        var resolved = input with
        {
            Data = input.Data with { CategoryId = category.CreatedEntityId }
        };
        var errors = new List<McpImportItemError>();
        var command = BuildCommand(resolved, errors);
        var state = errors.Count == 0
            ? McpImportValidationState.Valid
            : McpImportValidationState.Invalid;
        if (state == McpImportValidationState.Valid && command is not null)
        {
            var preparation = await _domain.PrepareAsync(
                context.UserId,
                command,
                cancellationToken);
            if (!preparation.IsReady)
            {
                state = McpImportValidationState.Invalid;
                errors.AddRange(preparation.Errors.Select(error =>
                    new McpImportItemError(
                        error.Code,
                        error.Message,
                        error.Field,
                        null)));
            }
        }

        var fingerprint = ImportFingerprint(context.UserId, resolved);
        if (state == McpImportValidationState.Valid)
        {
            var duplicates = await _imports.FindOwnedByFingerprintAsync(
                context.UserId,
                fingerprint,
                10,
                cancellationToken);
            if (duplicates.Any(candidate =>
                    candidate.Id != item.Id &&
                    candidate.ExecutionState is
                        McpImportExecutionState.Completed or
                        McpImportExecutionState.AlreadyApplied))
            {
                state = resolved.DuplicateDecision switch
                {
                    Application.Mcp.Models.McpImportDuplicateDecision.Skip =>
                        McpImportValidationState.Skipped,
                    Application.Mcp.Models.McpImportDuplicateDecision.ImportAnyway =>
                        McpImportValidationState.Valid,
                    _ => McpImportValidationState.PossibleDuplicate
                };
                if (state == McpImportValidationState.PossibleDuplicate)
                {
                    errors.Add(new McpImportItemError(
                        "POSSIBLE_DUPLICATE",
                        "O item pode duplicar um registro do mesmo proprietário.",
                        "duplicateDecision",
                        "Escolha explicitamente skip ou import_anyway."));
                }
            }
        }

        var version = item.Version;
        item.UpdateValidation(
            _protector.Protect(JsonSerializer.SerializeToUtf8Bytes(resolved)),
            fingerprint,
            state,
            errors);
        await _imports.ReplaceItemAsync(
            item,
            context.UserId,
            version,
            cancellationToken);
    }

    private static McpToolEnvelope<McpImportStatusData> StatusEnvelope(
        McpCallContext context,
        McpImportBatch batch,
        IReadOnlyList<McpImportItem> items)
    {
        var counts = Count(items);
        var status = StateWire(batch.State);
        var data = new McpImportStatusData(
            batch.Id,
            status,
            batch.ParentBatchId,
            MapCounts(counts),
            MapTotals(batch.Totals),
            items.Select(MapItem).ToArray(),
            counts["unknown"] > 0
                ? ["Há item com resultado desconhecido; consulte o status e aguarde reconciliação antes de nova tentativa."]
                : []);
        return new McpToolEnvelope<McpImportStatusData>(
            "1.0",
            context.CorrelationId,
            status,
            data,
            null,
            "BRL",
            new Dictionary<string, object?> { ["itemCount"] = items.Count },
            null,
            [],
            []);
    }

    private async Task<IReadOnlyList<McpImportItem>> ReconcileUnknownAsync(
        McpCallContext context,
        McpImportBatch batch,
        IReadOnlyList<McpImportItem> items,
        CancellationToken cancellationToken)
    {
        foreach (var item in items.Where(candidate =>
                     candidate.ExecutionState == McpImportExecutionState.Unknown &&
                     !string.IsNullOrWhiteSpace(candidate.OperationId)))
        {
            McpImportItemInput? input;
            try
            {
                input = JsonSerializer.Deserialize<McpImportItemInput>(
                    _protector.Unprotect(item.NormalizedDataCiphertext));
            }
            catch
            {
                continue;
            }
            if (input is null)
                continue;
            var errors = new List<McpImportItemError>();
            var command = BuildCommand(input, errors);
            if (command is null)
                continue;
            var effect = await _domain.FindEffectAsync(
                context.UserId,
                command,
                item.OperationId!,
                cancellationToken);
            if (effect?.State != McpDomainEffectState.Completed)
                continue;

            var version = item.Version;
            item.RecordResult(
                item.OperationId!,
                effect.EntityId,
                _time.GetUtcNow().UtcDateTime);
            await _imports.ReplaceItemAsync(
                item,
                context.UserId,
                version,
                cancellationToken);
        }

        var refreshed = await _imports.ListOwnedItemsAsync(
            batch.Id,
            context.UserId,
            cancellationToken);
        if (batch.State is McpImportBatchState.Partial or McpImportBatchState.Failed)
        {
            var counts = Count(refreshed);
            var state = counts["unknown"] > 0 || counts["failed"] > 0 ||
                        counts["execution_pending"] > 0
                ? counts["completed"] > 0
                    ? McpImportBatchState.Partial
                    : McpImportBatchState.Failed
                : McpImportBatchState.Completed;
            var version = batch.Version;
            batch.Reconcile(state, counts, _time.GetUtcNow().UtcDateTime);
            await _imports.ReplaceBatchAsync(
                batch,
                context.UserId,
                version,
                cancellationToken);
        }
        return refreshed;
    }

    private async Task<McpToolEnvelope<T>> AuditInvocationAsync<T>(
        McpCallContext context,
        string toolName,
        McpOperationClass operationClass,
        IReadOnlyDictionary<string, object?> sanitizedParameters,
        Func<Task<McpToolEnvelope<T>>> action,
        CancellationToken cancellationToken)
    {
        var invocation = McpOperationJournal.Start(
            context.UserId,
            context.ConnectionId,
            context.CorrelationId,
            toolName,
            operationClass,
            sanitizedParameters,
            Origin(context),
            startedAtUtc: _time.GetUtcNow().UtcDateTime);
        McpJournalCreateResult created;
        try
        {
            created = await _operations.CreateOrGetAsync(
                invocation,
                cancellationToken);
        }
        catch (Exception exception)
        {
            throw new McpJournalUnavailableException(exception);
        }

        invocation = created.Journal;
        if (!created.Created || created.RequestConflict)
        {
            throw new McpJournalUnavailableException(
                new InvalidOperationException(
                    "Não foi possível criar o journal da chamada de importação."));
        }

        try
        {
            var response = await action();
            var expectedVersion = invocation.Version;
            var summary = ImportInvocationSummary(response);
            if (string.Equals(response.Status, "rejected", StringComparison.Ordinal))
            {
                invocation.SetResultSummary(summary);
                invocation.Reject(
                    response.Errors.FirstOrDefault()?.Code ?? "IMPORT_REJECTED",
                    _time.GetUtcNow().UtcDateTime);
            }
            else
            {
                invocation.CompleteAt(
                    summary,
                    _time.GetUtcNow().UtcDateTime);
            }
            if (!await _operations.ReplaceAsync(
                    invocation,
                    expectedVersion,
                    cancellationToken))
            {
                throw new McpJournalUnavailableException(
                    new InvalidOperationException(
                        "O journal da chamada de importação não foi finalizado."));
            }
            return response;
        }
        catch (McpJournalUnavailableException)
        {
            throw;
        }
        catch (Exception)
        {
            var expectedVersion = invocation.Version;
            invocation.FailAt(
                "IMPORT_INVOCATION_FAILED",
                _time.GetUtcNow().UtcDateTime);
            await _operations.ReplaceAsync(
                invocation,
                expectedVersion,
                cancellationToken);
            throw;
        }
    }

    private static IReadOnlyDictionary<string, object?> ImportInvocationSummary<T>(
        McpToolEnvelope<T> response)
    {
        var summary = new Dictionary<string, object?>
        {
            ["status"] = response.Status,
            ["errorCode"] = response.Errors.FirstOrDefault()?.Code
        };
        switch (response.Data)
        {
            case McpImportPreviewData preview:
                summary["count"] = preview.Counts.Total;
                summary["importBatch"] = ImportBatchSummary(
                    preview.BatchId,
                    preview.State,
                    preview.Counts,
                    preview.Totals,
                    preview.Items);
                break;
            case McpImportStatusData status:
                summary["count"] = status.Counts.Total;
                summary["importBatch"] = ImportBatchSummary(
                    status.BatchId,
                    status.State,
                    status.Counts,
                    status.Totals,
                    status.Items);
                break;
        }
        return summary;
    }

    private static IReadOnlyDictionary<string, object?> ImportBatchSummary(
        string batchId,
        string state,
        McpImportCounts counts,
        McpImportTotals totals,
        IReadOnlyList<McpImportItemResult> items)
    {
        var countsByState = new Dictionary<string, object?>
        {
            ["valid"] = counts.Valid,
            ["invalid"] = counts.Invalid,
            ["pending"] = counts.Pending,
            ["possible_duplicate"] = counts.PossibleDuplicate,
            ["skipped"] = counts.Skipped,
            ["completed"] = counts.Completed,
            ["failed"] = counts.Failed,
            ["unknown"] = counts.Unknown
        };
        var countsByType = items
            .GroupBy(item => item.Type, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (object?)group.Count(),
                StringComparer.Ordinal);
        var safeTotals = new[]
            {
                ("income", totals.Income),
                ("expense", totals.Expense),
                ("investment", totals.Investment),
                ("fixed_cost", totals.FixedCost)
            }
            .Select(entry => new Dictionary<string, object?>
            {
                ["type"] = entry.Item1,
                ["amount"] = decimal.Parse(
                    entry.Item2,
                    CultureInfo.InvariantCulture),
                ["currency"] = "BRL"
            })
            .ToArray();
        var failures = items
            .SelectMany(item => item.Errors.Select(error =>
                new Dictionary<string, object?>
                {
                    ["clientItemId"] = item.ClientItemId,
                    ["sourceRef"] = item.SourceRef,
                    ["field"] = error.Field,
                    ["code"] = error.Code,
                    ["message"] = error.Message,
                    ["guidance"] = item.Suggestions.FirstOrDefault() ??
                                   "Revise este item antes de reenviá-lo."
                }))
            .ToArray();
        var results = items
            .Where(item =>
                item.ExecutionState is "completed" or "failed" or "unknown" &&
                !string.IsNullOrWhiteSpace(item.OperationId))
            .Select(item => new Dictionary<string, object?>
            {
                ["clientItemId"] = item.ClientItemId,
                ["sourceRef"] = item.SourceRef,
                ["type"] = item.Type,
                ["operationId"] = item.OperationId,
                ["result"] = item.ExecutionState
            })
            .ToArray();
        return new Dictionary<string, object?>
        {
            ["batchId"] = batchId,
            ["state"] = state,
            ["itemCount"] = counts.Total,
            ["countsByState"] = countsByState,
            ["countsByType"] = countsByType,
            ["totals"] = safeTotals,
            ["failures"] = failures,
            ["items"] = results
        };
    }

    private static IReadOnlyDictionary<string, object?> Origin(
        McpCallContext context) =>
        new Dictionary<string, object?>
        {
            ["clientId"] = context.ClientId,
            ["protocolRevision"] = context.ProtocolRevision,
            ["channel"] = "mcp"
        };

    private static string DomainFailureGuidance(string code) =>
        code.Contains("CATEGORY", StringComparison.Ordinal)
            ? "Revise a categoria informada e reenvie somente este item."
            : code.Contains("CONFLICT", StringComparison.Ordinal)
                ? "Atualize os dados do item e reenvie somente este item."
                : "Revise os dados informados e reenvie somente este item.";

    private static McpToolEnvelope<McpImportPreviewData> RejectedPreview(
        McpCallContext context,
        string code,
        string message,
        string? field,
        IReadOnlyDictionary<string, object?>? details = null,
        IReadOnlyList<string>? guidance = null) =>
        new(
            "1.0",
            context.CorrelationId,
            "rejected",
            null,
            null,
            "BRL",
            new Dictionary<string, object?>(),
            null,
            guidance ?? [],
            [new McpToolError(code, message, field, false, details)]);

    private static McpToolEnvelope<McpImportStatusData> RejectedStatus(
        McpCallContext context,
        string code,
        string message) =>
        new(
            "1.0",
            context.CorrelationId,
            "rejected",
            null,
            null,
            "BRL",
            new Dictionary<string, object?>(),
            null,
            [],
            [new McpToolError(code, message, null, false)]);
}
