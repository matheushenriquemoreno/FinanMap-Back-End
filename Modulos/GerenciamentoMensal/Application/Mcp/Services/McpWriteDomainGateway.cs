#nullable enable

using System.Globalization;
using System.Text.Json;
using Application.CustoFixo.DTOs;
using Application.CustoFixo.Interfaces;
using Application.DTOs;
using Application.Interface;
using Application.Interfaces;
using Application.Mcp.Interfaces;
using Application.Mcp.Models;
using Domain.Enum;
using Domain.Enums;
using Domain.Mcp.Entities;
using Domain.Mcp.Enums;

namespace Application.Mcp.Services;

public sealed class McpWriteDomainGateway(
    ICategoriaService categories,
    IRendimentoService incomes,
    IDespesaService expenses,
    IInvestimentoService investments,
    ICustoFixoService fixedCosts,
    IMcpWriteEffectStore effects) : IMcpWriteDomainGateway
{
    public async Task<McpDomainPreparation> PrepareAsync(
        string userId,
        McpWriteCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Action == McpPreviewAction.Create)
            return await PrepareCreateAsync(userId, command, cancellationToken);

        var current = await effects.LoadOwnedAsync(
            command.Entity,
            command.TargetId!,
            userId,
            cancellationToken);
        if (current is null)
            return NotFound(command);

        if (command.Entity == McpWriteEntity.Category &&
            command.Action == McpPreviewAction.Delete &&
            await effects.CategoryHasLinksAsync(
                current.Id,
                userId,
                cancellationToken))
        {
            return McpDomainPreparation.Rejected(
                command,
                new McpToolError(
                    "DELETE_BLOCKED_RELATIONSHIP",
                    "A categoria possui registros vinculados. Remova ou altere os vínculos antes da exclusão.",
                    "id",
                    false));
        }

        if (command.Entity == McpWriteEntity.Expense &&
            command.Action == McpPreviewAction.Delete)
        {
            var groupedExpenses = await effects.ListGroupedExpensesAsync(
                current.Id,
                userId,
                cancellationToken);
            if (groupedExpenses.Count > 0)
                return PrepareGroupingParentDelete(command, current, groupedExpenses);
        }

        var proposed = command.Action == McpPreviewAction.Delete
            ? new Dictionary<string, object?>()
            : Merge(current.Values, command.Values);
        if (command.Entity == McpWriteEntity.Expense &&
            HasText(current.Values, "expenseOriginId") &&
            !HasText(command.Values, "batchModifier"))
        {
            return McpDomainPreparation.Rejected(
                command,
                new McpToolError(
                    "VALIDATION_REQUIRED",
                    "Informe se a alteração ou exclusão vale apenas para esta despesa, para esta e as próximas ou para todo o lote.",
                    "batchModifier",
                    false));
        }
        if (command.Action == McpPreviewAction.Update &&
            command.Entity != McpWriteEntity.Category)
        {
            var relationship = await ValidateRelationshipsAsync(
                userId,
                proposed,
                command,
                cancellationToken);
            if (relationship is not null)
                return relationship;
        }

        if (command.Entity == McpWriteEntity.Expense &&
            HasText(current.Values, "expenseOriginId") &&
            TryBatchModifier(command.Values, out var modifier))
        {
            return await PrepareExpenseBatchAsync(
                userId,
                command,
                current,
                modifier,
                cancellationToken);
        }

        var normalized = command with
        {
            Values = new Dictionary<string, object?>(command.Values),
            ExpectedValues = new Dictionary<string, object?>(current.Values)
        };
        if (command.Entity == McpWriteEntity.Expense)
        {
            normalized = await AppendGroupingSyncStepsAsync(
                userId,
                normalized,
                [
                    Text(current.Values, "groupingExpenseId"),
                    Text(proposed, "groupingExpenseId")
                ],
                cancellationToken);
        }
        return McpDomainPreparation.Ready(
            normalized,
            current.Values,
            proposed,
            [
                new McpSnapshotHash(
                    EntityWire(command.Entity),
                    current.Id,
                    McpCursorCodec.CanonicalFingerprint(current.Values))
            ]);
    }

    private async Task<McpDomainPreparation> PrepareExpenseBatchAsync(
        string userId,
        McpWriteCommand command,
        McpWriteStoredRecord current,
        ModificadorLote modifier,
        CancellationToken cancellationToken)
    {
        var batchId = current.Values["expenseOriginId"]!.ToString()!;
        var batch = await effects.ListExpenseBatchAsync(
            batchId,
            userId,
            cancellationToken);
        var ordered = batch
            .OrderBy(item => Integer(item.Values.GetValueOrDefault("year")))
            .ThenBy(item => Integer(item.Values.GetValueOrDefault("month")))
            .ThenBy(item => Integer(item.Values.GetValueOrDefault("installmentNumber")))
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        var targetIndex = Array.FindIndex(
            ordered,
            item => string.Equals(item.Id, current.Id, StringComparison.Ordinal));
        if (targetIndex < 0)
            return NotFound(command);
        var selected = modifier switch
        {
            ModificadorLote.ApenasEsta => [ordered[targetIndex]],
            ModificadorLote.EstaEProximas => ordered[targetIndex..],
            ModificadorLote.TodasDoLote => ordered,
            _ => []
        };
        if (selected.Length == 0)
        {
            return McpDomainPreparation.Rejected(
                command,
                new McpToolError(
                    "VALIDATION_REQUIRED",
                    "Informe um alcance válido para o lote de despesas.",
                    "batchModifier",
                    false));
        }

        var changes = command.Values
            .Where(item => item.Key != "batchModifier")
            .ToDictionary(item => item.Key, item => item.Value);
        var steps = selected
            .Select((record, index) =>
                new McpWriteStepPlan(
                    $"expense-{index + 1:000}",
                    record.Id,
                    command.Action == McpPreviewAction.Delete
                        ? new Dictionary<string, object?>()
                        : new Dictionary<string, object?>(changes),
                    new Dictionary<string, object?>(record.Values)))
            .ToArray();
        var snapshots = selected
            .Select(record => new McpSnapshotHash(
                "expense",
                record.Id,
                McpCursorCodec.CanonicalFingerprint(record.Values)))
            .ToArray();
        var normalized = command with
        {
            Values = changes,
            ExpectedValues = new Dictionary<string, object?>(current.Values),
            Steps = steps
        };
        normalized = await AppendGroupingSyncStepsAsync(
            userId,
            normalized,
            selected
                .SelectMany(record =>
                {
                    var oldGrouping = Text(record.Values, "groupingExpenseId");
                    var newGrouping = command.Action == McpPreviewAction.Update &&
                                      changes.ContainsKey("groupingExpenseId")
                        ? Text(changes, "groupingExpenseId")
                        : oldGrouping;
                    return new[] { oldGrouping, newGrouping };
                }),
            cancellationToken);
        var proposed = command.Action == McpPreviewAction.Delete
            ? new Dictionary<string, object?>()
            : Merge(current.Values, changes);
        return McpDomainPreparation.Ready(
            normalized,
            current.Values,
            new Dictionary<string, object?>(proposed)
            {
                ["affectedCount"] = selected.Length,
                ["batchModifier"] = modifier.ToString()
            },
            snapshots);
    }

    private static McpDomainPreparation PrepareGroupingParentDelete(
        McpWriteCommand command,
        McpWriteStoredRecord parent,
        IReadOnlyList<McpWriteStoredRecord> children)
    {
        var orderedTargets = children
            .OrderBy(child => child.Id, StringComparer.Ordinal)
            .Append(parent)
            .ToArray();
        var steps = orderedTargets
            .Select((record, index) => new McpWriteStepPlan(
                $"expense-{index + 1:000}",
                record.Id,
                new Dictionary<string, object?>(),
                new Dictionary<string, object?>(record.Values)))
            .ToArray();
        var snapshots = orderedTargets
            .Select(record => new McpSnapshotHash(
                "expense",
                record.Id,
                McpCursorCodec.CanonicalFingerprint(record.Values)))
            .ToArray();
        var normalized = command with
        {
            Values = new Dictionary<string, object?>(),
            ExpectedValues = new Dictionary<string, object?>(parent.Values),
            Steps = steps
        };
        return McpDomainPreparation.Ready(
            normalized,
            parent.Values,
            new Dictionary<string, object?>
            {
                ["affectedCount"] = orderedTargets.Length,
                ["groupingDelete"] = true
            },
            snapshots);
    }

    public async Task<McpDomainEffect> ExecuteAsync(
        string userId,
        McpWriteCommand command,
        string operationId,
        IReadOnlyList<McpSnapshotHash> snapshots,
        CancellationToken cancellationToken = default)
    {
        var prior = await effects.FindEffectAsync(
            command.Entity,
            userId,
            operationId,
            cancellationToken);
        if (prior is not null)
            return EffectFromRecord(prior, operationId, command.Action);

        if (command.Entity == McpWriteEntity.Expense &&
            string.Equals(command.StepType, "expense_group_sync", StringComparison.Ordinal))
        {
            var expectedParentAmount = Money(command.Values["expectedParentAmount"]);
            var baseAmount = Money(command.Values["baseAmount"]);
            var groupResultHash = McpCursorCodec.CanonicalFingerprint(command.Values);
            var groupMutation = await expenses.SincronizarAgrupamentoMcpAsync(
                command.TargetId!,
                expectedParentAmount,
                baseAmount,
                operationId,
                groupResultHash,
                cancellationToken);
            return EffectFromMutation(
                groupMutation,
                McpWriteEntity.Expense,
                McpPreviewAction.Update,
                operationId);
        }

        if (command.Action == McpPreviewAction.Create)
            return await ExecuteCreateAsync(command, operationId);

        if (command.ExpectedValues is null || command.TargetId is null)
        {
            return McpDomainEffect.Rejected(
                "PREVIEW_PAYLOAD_INVALID",
                "A prévia não contém o snapshot necessário.");
        }

        var resultHash = McpCursorCodec.CanonicalFingerprint(
            command.Action == McpPreviewAction.Update
                ? Merge(command.ExpectedValues, command.Values)
                : command.ExpectedValues);
        var mutation = command.Entity switch
        {
            McpWriteEntity.Category =>
                await categories.AplicarMutacaoMcpAsync(
                    command,
                    operationId,
                    resultHash,
                    cancellationToken),
            McpWriteEntity.Income =>
                await incomes.AplicarMutacaoMcpAsync(
                    command,
                    operationId,
                    resultHash,
                    cancellationToken),
            McpWriteEntity.Expense =>
                await expenses.AplicarMutacaoMcpAsync(
                    command,
                    operationId,
                    resultHash,
                    cancellationToken),
            McpWriteEntity.Investment =>
                await investments.AplicarMutacaoMcpAsync(
                    command,
                    operationId,
                    resultHash,
                    cancellationToken),
            McpWriteEntity.FixedCost =>
                await fixedCosts.AplicarMutacaoMcpAsync(
                    command,
                    operationId,
                    resultHash,
                    cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
        return EffectFromMutation(
            mutation,
            command.Entity,
            command.Action,
            operationId);
    }

    public async Task<McpDomainEffect?> FindEffectAsync(
        string userId,
        McpWriteCommand command,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        var record = await effects.FindEffectAsync(
            command.Entity,
            userId,
            operationId,
            cancellationToken);
        if (record is not null)
            return EffectFromRecord(record, operationId, command.Action);

        if (command.Action != McpPreviewAction.Delete ||
            string.IsNullOrWhiteSpace(command.TargetId))
        {
            return null;
        }

        var target = await effects.LoadOwnedAsync(
            command.Entity,
            command.TargetId,
            userId,
            cancellationToken);
        return target is null
            ? McpDomainEffect.Unknown(
                "O registro está ausente, mas a causalidade da exclusão não pôde ser comprovada. Não tente novamente; consulte o status.")
            : null;
    }

    private async Task<McpDomainPreparation> PrepareCreateAsync(
        string userId,
        McpWriteCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Entity == McpWriteEntity.Category)
        {
            if (!TryCategoryType(command.Values["type"]?.ToString(), out var type))
            {
                return McpDomainPreparation.Rejected(
                    command,
                    new McpToolError(
                        "VALIDATION_REQUIRED",
                        "O tipo de categoria é inválido.",
                        "type",
                        false));
            }
            var name = command.Values["name"]?.ToString() ?? string.Empty;
            var existing = await categories.ObterCategoria(type, name);
            if (!existing.IsFailure &&
                existing.Value.Any(item =>
                    string.Equals(
                        item.Nome,
                        name,
                        StringComparison.OrdinalIgnoreCase)))
            {
                return McpDomainPreparation.Rejected(
                    command,
                    new McpToolError(
                        "DOMAIN_DUPLICATE",
                        "Já existe uma categoria com esse nome e tipo.",
                        "name",
                        false));
            }
        }
        else
        {
            var relationship = await ValidateRelationshipsAsync(
                userId,
                command.Values,
                command,
                cancellationToken);
            if (relationship is not null)
                return relationship;
        }

        if (command.Entity == McpWriteEntity.Expense &&
            command.Action == McpPreviewAction.Create &&
            Integer(command.Values.GetValueOrDefault("recurrenceCount")) > 1)
        {
            command = PlanExpenseCreation(command);
        }
        if (command.Entity == McpWriteEntity.Expense)
        {
            command = await AppendGroupingSyncStepsAsync(
                userId,
                command,
                [Text(command.Values, "groupingExpenseId")],
                cancellationToken);
        }

        return McpDomainPreparation.Ready(
            command,
            null,
            command.Values,
            []);
    }

    private async Task<McpDomainPreparation?> ValidateRelationshipsAsync(
        string userId,
        IReadOnlyDictionary<string, object?> values,
        McpWriteCommand command,
        CancellationToken cancellationToken)
    {
        var categoryId = values.GetValueOrDefault("categoryId")?.ToString();
        if (command.Entity == McpWriteEntity.FixedCost &&
            string.IsNullOrWhiteSpace(categoryId))
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            return McpDomainPreparation.Rejected(
                command,
                new McpToolError(
                    "VALIDATION_REQUIRED",
                    "Informe a categoria do registro financeiro.",
                    "categoryId",
                    false));
        }
        var category = await effects.LoadOwnedAsync(
            McpWriteEntity.Category,
            categoryId,
            userId,
            cancellationToken);
        if (category is null)
        {
            return McpDomainPreparation.Rejected(
                command,
                ActionableError(
                    "RECORD_NOT_FOUND",
                    "A categoria não foi encontrada para esta conta.",
                    "categoryId",
                    "Revise categoryId, selecione uma categoria visível nesta conta e prepare uma nova prévia."));
        }
        if (!string.Equals(
                category.Values.GetValueOrDefault("type")?.ToString(),
                ExpectedCategoryType(command.Entity).ToString(),
                StringComparison.Ordinal))
        {
            return McpDomainPreparation.Rejected(
                command,
                ActionableError(
                    "CATEGORY_RELATIONSHIP_INVALID",
                    "A categoria não é compatível com este tipo financeiro.",
                    "categoryId",
                    "Selecione uma categoria compatível com o tipo financeiro e prepare uma nova prévia."));
        }

        if (command.Entity == McpWriteEntity.Expense &&
            values.TryGetValue("groupingExpenseId", out var groupingValue) &&
            !string.IsNullOrWhiteSpace(groupingValue?.ToString()))
        {
            var groupingId = groupingValue!.ToString()!;
            if (string.Equals(groupingId, command.TargetId, StringComparison.Ordinal))
            {
                return McpDomainPreparation.Rejected(
                    command,
                    ActionableError(
                        "GROUPING_RELATIONSHIP_INVALID",
                        "Uma despesa não pode agrupar a si mesma.",
                        "groupingExpenseId",
                        "Escolha uma despesa agrupadora diferente da despesa alterada e prepare uma nova prévia."));
            }
            var grouping = await effects.LoadOwnedAsync(
                McpWriteEntity.Expense,
                groupingId,
                userId,
                cancellationToken);
            if (grouping is null)
            {
                return McpDomainPreparation.Rejected(
                    command,
                    ActionableError(
                        "RECORD_NOT_FOUND",
                        "A despesa agrupadora não foi encontrada para esta conta.",
                        "groupingExpenseId",
                        "Revise groupingExpenseId, selecione uma despesa visível nesta conta e prepare uma nova prévia."));
            }
        }
        return null;
    }

    private async Task<McpDomainEffect> ExecuteCreateAsync(
        McpWriteCommand command,
        string operationId)
    {
        if (command.Entity == McpWriteEntity.Category)
        {
            if (!TryCategoryType(command.Values["type"]?.ToString(), out var type))
                return McpDomainEffect.Rejected("VALIDATION_REQUIRED", "Tipo de categoria inválido.");
            var result = await categories.Adicionar(new CreateCategoriaDTO
            {
                Nome = command.Values["name"]?.ToString() ?? string.Empty,
                Tipo = type,
                McpOperationId = operationId
            });
            if (result.IsFailure)
                return McpDomainEffect.Rejected("DOMAIN_REJECTED", result.Error.Message);
            return McpDomainEffect.Completed(
                result.Value.Id,
                operationId,
                Result(
                    McpWriteEntity.Category,
                    result.Value.Id,
                    "create",
                    "Categoria criada."));
        }

        if (command.Entity == McpWriteEntity.Income)
        {
            var incomeResult = await incomes.Adicionar(new CreateRendimentoDTO
            {
                Ano = Integer(command.Values["year"]),
                Mes = Integer(command.Values["month"]),
                Descricao = command.Values["description"]?.ToString() ?? string.Empty,
                Valor = Money(command.Values["amount"]),
                CategoriaId = command.Values["categoryId"]?.ToString() ?? string.Empty,
                McpOperationId = operationId
            });
            if (incomeResult.IsFailure)
                return McpDomainEffect.Rejected("DOMAIN_REJECTED", incomeResult.Error.Message);
            return McpDomainEffect.Completed(
                incomeResult.Value.Id,
                operationId,
                Result(
                    McpWriteEntity.Income,
                    incomeResult.Value.Id,
                    "create",
                    "Receita criada."));
        }

        if (command.Entity == McpWriteEntity.Expense)
        {
            var result = await expenses.Adicionar(new CreateDespesaDTO
            {
                Ano = Integer(command.Values["year"]),
                Mes = Integer(command.Values["month"]),
                Descricao = command.Values["description"]?.ToString() ?? string.Empty,
                Valor = Money(command.Values["amount"]),
                CategoriaId = command.Values["categoryId"]?.ToString() ?? string.Empty,
                IdDespesaAgrupadora = Text(command.Values, "groupingExpenseId"),
                DespesaOrigemId = Text(command.Values, "expenseOriginId"),
                IsParcelado = Boolean(command.Values.GetValueOrDefault("isInstallment")),
                IsRecorrente = Boolean(command.Values.GetValueOrDefault("isRecurring")),
                ParcelaAtual = NullableInteger(command.Values.GetValueOrDefault("installmentNumber")),
                TotalParcelas = NullableInteger(command.Values.GetValueOrDefault("installmentCount")),
                McpOperationId = operationId
            });
            if (result.IsFailure)
                return DomainRejected(result.Error.Message);
            return Created(command.Entity, result.Value.Id, operationId, "Despesa criada.");
        }

        if (command.Entity == McpWriteEntity.Investment)
        {
            var result = await investments.Adicionar(new CreateInvestimentoDTO
            {
                Ano = Integer(command.Values["year"]),
                Mes = Integer(command.Values["month"]),
                Descricao = command.Values["description"]?.ToString() ?? string.Empty,
                Valor = Money(command.Values["amount"]),
                CategoriaId = command.Values["categoryId"]?.ToString() ?? string.Empty,
                McpOperationId = operationId
            });
            if (result.IsFailure)
                return DomainRejected(result.Error.Message);
            return Created(command.Entity, result.Value.Id, operationId, "Investimento criado.");
        }

        var fixedCostResult = await fixedCosts.Adicionar(new CreateCustoFixoDTO
        {
            Nome = command.Values["name"]?.ToString() ?? string.Empty,
            DiaVencimento = Integer(command.Values["dueDay"]),
            CategoriaId = Text(command.Values, "categoryId"),
            McpOperationId = operationId
        });
        if (fixedCostResult.IsFailure)
            return DomainRejected(fixedCostResult.Error.Message);
        return Created(
            command.Entity,
            fixedCostResult.Value.Id,
            operationId,
            "Custo fixo criado.");
    }

    private static McpDomainPreparation NotFound(McpWriteCommand command) =>
        McpDomainPreparation.Rejected(
            command,
            ActionableError(
                "RECORD_NOT_FOUND",
                "O registro não foi encontrado para esta conta.",
                "id",
                "Revise id, selecione um registro visível nesta conta e prepare uma nova prévia."));

    private static McpToolError ActionableError(
        string code,
        string message,
        string field,
        string guidance) =>
        new(
            code,
            message,
            field,
            false,
            new Dictionary<string, object?>
            {
                ["guidance"] = guidance
            });

    private static McpDomainEffect EffectFromRecord(
        McpWriteStoredRecord record,
        string operationId,
        McpPreviewAction action) =>
        McpDomainEffect.Completed(
            record.Id,
            operationId,
            Result(
                record.Entity,
                record.Id,
                action.ToString().ToLowerInvariant(),
                action switch
                {
                    McpPreviewAction.Create => "Registro criado.",
                    McpPreviewAction.Update => "Registro alterado.",
                    _ => "Registro excluído."
                }));

    private static McpDomainEffect EffectFromMutation(
        McpApplicationMutationResult mutation,
        McpWriteEntity entity,
        McpPreviewAction action,
        string operationId) =>
        mutation.State switch
        {
            McpApplicationMutationState.Completed =>
                McpDomainEffect.Completed(
                    mutation.EntityId ??
                    throw new InvalidOperationException(
                        "Mutação MCP concluída sem referência de entidade."),
                    mutation.EffectMarker ?? operationId,
                    Result(
                        entity,
                        mutation.EntityId,
                        action.ToString().ToLowerInvariant(),
                        action == McpPreviewAction.Update
                            ? "Registro alterado."
                            : "Registro excluído definitivamente.")),
            McpApplicationMutationState.ConflictChanged =>
                McpDomainEffect.ConflictChanged(),
            McpApplicationMutationState.Unknown =>
                McpDomainEffect.Unknown(
                    mutation.Message ??
                    "A causalidade do efeito não pôde ser comprovada."),
            _ => McpDomainEffect.Rejected(
                mutation.ErrorCode ?? "DOMAIN_REJECTED",
                mutation.Message ??
                "A operação foi rejeitada pelas regras do FinanMap.")
        };

    private static IReadOnlyDictionary<string, object?> Result(
        McpWriteEntity entity,
        string id,
        string action,
        string summary) =>
        new Dictionary<string, object?>
        {
            ["entityType"] = EntityWire(entity),
            ["entityId"] = id,
            ["action"] = action,
            ["reference"] = id,
            ["summary"] = summary
        };

    private static IReadOnlyDictionary<string, object?> Merge(
        IReadOnlyDictionary<string, object?> current,
        IReadOnlyDictionary<string, object?> changes)
    {
        var merged = new Dictionary<string, object?>(current);
        foreach (var change in changes)
            merged[change.Key] = change.Value;
        return merged;
    }

    private static bool TryCategoryType(string? value, out TipoCategoria type) =>
        Enum.TryParse(value, true, out type) && Enum.IsDefined(type);

    private static TipoCategoria ExpectedCategoryType(McpWriteEntity entity) =>
        entity switch
        {
            McpWriteEntity.Income => TipoCategoria.Rendimento,
            McpWriteEntity.Investment => TipoCategoria.Investimento,
            McpWriteEntity.Expense or McpWriteEntity.FixedCost => TipoCategoria.Despesa,
            _ => throw new ArgumentOutOfRangeException(nameof(entity))
        };

    private static bool HasText(
        IReadOnlyDictionary<string, object?> values,
        string field) =>
        values.TryGetValue(field, out var value) &&
        !string.IsNullOrWhiteSpace(value?.ToString());

    private static bool TryBatchModifier(
        IReadOnlyDictionary<string, object?> values,
        out ModificadorLote modifier) =>
        Enum.TryParse(
            values.GetValueOrDefault("batchModifier")?.ToString(),
            true,
            out modifier) &&
        Enum.IsDefined(modifier);

    private static int Integer(object? value) =>
        value switch
        {
            JsonElement { ValueKind: JsonValueKind.Number } element =>
                element.GetInt32(),
            JsonElement { ValueKind: JsonValueKind.String } element
                when int.TryParse(
                    element.GetString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsed) =>
                parsed,
            _ => Convert.ToInt32(value, CultureInfo.InvariantCulture)
        };

    private static int? NullableInteger(object? value) =>
        value is null ? null : Integer(value);

    private static decimal Money(object? value) =>
        decimal.Parse(
            value?.ToString() ?? "0",
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture);

    private static string? Text(
        IReadOnlyDictionary<string, object?> values,
        string field) =>
        values.TryGetValue(field, out var value) &&
        !string.IsNullOrWhiteSpace(value?.ToString())
            ? value!.ToString()
            : null;

    private static McpDomainEffect DomainRejected(string message) =>
        McpDomainEffect.Rejected("DOMAIN_REJECTED", message);

    private static McpDomainEffect Created(
        McpWriteEntity entity,
        string id,
        string operationId,
        string summary) =>
        McpDomainEffect.Completed(
            id,
            operationId,
            Result(entity, id, "create", summary));

    private static McpWriteCommand PlanExpenseCreation(McpWriteCommand command)
    {
        var count = Integer(command.Values["recurrenceCount"]);
        var year = Integer(command.Values["year"]);
        var month = Integer(command.Values["month"]);
        var total = decimal.Parse(
            command.Values["amount"]!.ToString()!,
            CultureInfo.InvariantCulture);
        var installment = Boolean(command.Values.GetValueOrDefault("isInstallment"));
        var batchId = McpCursorCodec.CanonicalFingerprint(command.Values)[..24];
        var baseAmount = installment
            ? Math.Round(total / count, 2, MidpointRounding.AwayFromZero)
            : total;
        var accumulated = 0m;
        var steps = new List<McpWriteStepPlan>(count);
        for (var index = 0; index < count; index++)
        {
            var stepValues = new Dictionary<string, object?>(command.Values);
            var offset = (month - 1) + index;
            stepValues["year"] = year + (offset / 12);
            stepValues["month"] = (offset % 12) + 1;
            var amount = installment && index == count - 1
                ? total - accumulated
                : baseAmount;
            accumulated += amount;
            stepValues["amount"] = amount.ToString(
                "0.00",
                CultureInfo.InvariantCulture);
            stepValues["expenseOriginId"] = batchId;
            stepValues["installmentNumber"] = installment ? index + 1 : null;
            stepValues["installmentCount"] = installment ? count : null;
            steps.Add(
                new McpWriteStepPlan(
                    $"expense-{index + 1:000}",
                    null,
                    stepValues,
                    null));
        }
        return command with
        {
            Values = new Dictionary<string, object?>(command.Values)
            {
                ["expenseOriginId"] = batchId
            },
            Steps = steps
        };
    }

    private async Task<McpWriteCommand> AppendGroupingSyncStepsAsync(
        string userId,
        McpWriteCommand command,
        IEnumerable<string?> groupingIds,
        CancellationToken cancellationToken)
    {
        var distinctIds = groupingIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (distinctIds.Length == 0)
            return command;

        var steps = command.Steps is { Count: > 0 }
            ? command.Steps.ToList()
            :
            [
                new McpWriteStepPlan(
                    "expense-001",
                    command.TargetId,
                    command.Values,
                    command.ExpectedValues)
            ];
        foreach (var groupingId in distinctIds)
        {
            var parent = await effects.LoadOwnedAsync(
                McpWriteEntity.Expense,
                groupingId!,
                userId,
                cancellationToken);
            if (parent is null)
                continue;
            var children = await effects.ListGroupedExpensesAsync(
                groupingId!,
                userId,
                cancellationToken);
            var expectedAmount = Money(parent.Values["amount"]);
            var childTotal = children.Sum(item => Money(item.Values["amount"]));
            steps.Add(new McpWriteStepPlan(
                $"group-sync-{steps.Count + 1:000}",
                groupingId,
                new Dictionary<string, object?>
                {
                    ["expectedParentAmount"] = expectedAmount.ToString("0.00", CultureInfo.InvariantCulture),
                    ["baseAmount"] = (expectedAmount - childTotal).ToString("0.00", CultureInfo.InvariantCulture)
                },
                new Dictionary<string, object?>(parent.Values),
                "expense_group_sync"));
        }
        return command with { Steps = steps };
    }

    private static bool Boolean(object? value) =>
        value switch
        {
            bool boolean => boolean,
            JsonElement { ValueKind: JsonValueKind.True } => true,
            JsonElement { ValueKind: JsonValueKind.False } => false,
            _ => bool.TryParse(value?.ToString(), out var parsed) && parsed
        };

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
}
