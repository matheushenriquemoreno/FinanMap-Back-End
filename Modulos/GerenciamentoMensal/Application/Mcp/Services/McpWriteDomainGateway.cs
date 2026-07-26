#nullable enable

using System.Globalization;
using System.Text.Json;
using Application.DTOs;
using Application.Interface;
using Application.Interfaces;
using Application.Mcp.Interfaces;
using Application.Mcp.Models;
using Domain.Enum;
using Domain.Mcp.Entities;
using Domain.Mcp.Enums;

namespace Application.Mcp.Services;

public sealed class McpWriteDomainGateway(
    ICategoriaService categories,
    IRendimentoService incomes,
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

        var proposed = command.Action == McpPreviewAction.Delete
            ? new Dictionary<string, object?>()
            : Merge(current.Values, command.Values);
        if (command.Entity == McpWriteEntity.Income &&
            command.Action == McpPreviewAction.Update)
        {
            var relationship = await ValidateIncomeCategoryAsync(
                userId,
                proposed,
                command,
                cancellationToken);
            if (relationship is not null)
                return relationship;
        }

        var normalized = command with
        {
            Values = new Dictionary<string, object?>(command.Values),
            ExpectedValues = new Dictionary<string, object?>(current.Values)
        };
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
        var mutation = command.Entity == McpWriteEntity.Category
            ? await categories.AplicarMutacaoMcpAsync(
                command,
                operationId,
                resultHash,
                cancellationToken)
            : await incomes.AplicarMutacaoMcpAsync(
                command,
                operationId,
                resultHash,
                cancellationToken);
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
            var relationship = await ValidateIncomeCategoryAsync(
                userId,
                command.Values,
                command,
                cancellationToken);
            if (relationship is not null)
                return relationship;
        }

        return McpDomainPreparation.Ready(
            command,
            null,
            command.Values,
            []);
    }

    private async Task<McpDomainPreparation?> ValidateIncomeCategoryAsync(
        string userId,
        IReadOnlyDictionary<string, object?> values,
        McpWriteCommand command,
        CancellationToken cancellationToken)
    {
        var categoryId = values.GetValueOrDefault("categoryId")?.ToString();
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            return McpDomainPreparation.Rejected(
                command,
                new McpToolError(
                    "VALIDATION_REQUIRED",
                    "Informe a categoria da receita.",
                    "categoryId",
                    false));
        }
        var category = await effects.LoadOwnedAsync(
            McpWriteEntity.Category,
            categoryId,
            userId,
            cancellationToken);
        if (category is null ||
            !string.Equals(
                category.Values.GetValueOrDefault("type")?.ToString(),
                TipoCategoria.Rendimento.ToString(),
                StringComparison.Ordinal))
        {
            return McpDomainPreparation.Rejected(
                command,
                new McpToolError(
                    "CATEGORY_RELATIONSHIP_INVALID",
                    "A categoria informada não existe nesta conta ou não é uma categoria de receita.",
                    "categoryId",
                    false));
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

        var incomeResult = await incomes.Adicionar(new CreateRendimentoDTO
        {
            Ano = Integer(command.Values["year"]),
            Mes = Integer(command.Values["month"]),
            Descricao = command.Values["description"]?.ToString() ?? string.Empty,
            Valor = decimal.Parse(
                command.Values["amount"]!.ToString()!,
                CultureInfo.InvariantCulture),
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

    private static McpDomainPreparation NotFound(McpWriteCommand command) =>
        McpDomainPreparation.Rejected(
            command,
            new McpToolError(
                "RECORD_NOT_FOUND",
                "O registro não foi encontrado para esta conta.",
                "id",
                false));

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

    private static string EntityWire(McpWriteEntity entity) =>
        entity == McpWriteEntity.Category ? "category" : "income";
}
