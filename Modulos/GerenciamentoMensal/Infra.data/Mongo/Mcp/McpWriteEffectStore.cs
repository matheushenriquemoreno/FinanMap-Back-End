#nullable enable

using System.Globalization;
using System.Text;
using Application.Mcp.Interfaces;
using Application.Mcp.Models;
using Domain.Enum;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Mcp;

public sealed class McpWriteEffectStore(IMongoClient mongoClient)
    : IMcpWriteEffectStore
{
    private readonly IMongoDatabase _database =
        mongoClient.GetDatabase("FinanMap");

    public async Task<McpWriteStoredRecord?> LoadOwnedAsync(
        McpWriteEntity entity,
        string id,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var collection = Collection(entity);
        var filter = OwnedFilter(id, userId);
        var document = await collection.Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : Map(entity, document);
    }

    public async Task<bool> CategoryHasLinksAsync(
        string id,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (await LoadOwnedAsync(
                McpWriteEntity.Category,
                id,
                userId,
                cancellationToken) is null)
        {
            return false;
        }

        var builder = Builders<BsonDocument>.Filter;
        var filter = IdFilter(builder, "CategoriaId", id) &
                     IdFilter(builder, "UsuarioId", userId);
        foreach (var collectionName in new[]
                 {
                     "Rendimento",
                     "Despesa",
                     "Investimento",
                     "CustoFixo"
                 })
        {
            if (await _database.GetCollection<BsonDocument>(collectionName)
                    .CountDocumentsAsync(
                        filter,
                        new CountOptions { Limit = 1 },
                        cancellationToken) > 0)
            {
                return true;
            }
        }
        return false;
    }

    public async Task<McpWriteStoredRecord?> FindEffectAsync(
        McpWriteEntity entity,
        string userId,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<BsonDocument>.Filter;
        var filter = IdFilter(builder, "UsuarioId", userId) &
                     builder.Or(
                         builder.Eq("McpOperationId", operationId),
                         builder.Eq("LastMcpOperationId", operationId));
        var document = await Collection(entity).Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : Map(entity, document);
    }

    public async Task<IReadOnlyList<McpWriteStoredRecord>> ListExpenseBatchAsync(
        string expenseOriginId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<BsonDocument>.Filter;
        var filter = builder.Eq("DespesaOrigemId", expenseOriginId) &
                     IdFilter(builder, "UsuarioId", userId);
        var documents = await Collection(McpWriteEntity.Expense)
            .Find(filter)
            .ToListAsync(cancellationToken);
        return documents
            .Select(document => Map(McpWriteEntity.Expense, document))
            .ToArray();
    }

    public async Task<IReadOnlyList<McpWriteStoredRecord>> ListGroupedExpensesAsync(
        string groupingExpenseId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var collection = Collection(McpWriteEntity.Expense);
        var builder = Builders<BsonDocument>.Filter;
        var filter = IdFilter(builder, "IdDespesaAgrupadora", groupingExpenseId) &
                     IdFilter(builder, "UsuarioId", userId);
        var documents = await collection.Find(filter).ToListAsync(cancellationToken);
        return documents
            .Select(document => Map(McpWriteEntity.Expense, document))
            .ToArray();
    }

    public async Task<bool> HasPossibleDuplicateAsync(
        string userId,
        McpWriteCommand command,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<BsonDocument>.Filter;
        var filter = IdFilter(builder, "UsuarioId", userId);
        if (command.Entity == McpWriteEntity.Category)
        {
            filter &= builder.Eq(
                "Tipo",
                Convert.ToInt32(command.Values["type"], CultureInfo.InvariantCulture));
        }
        else if (command.Entity == McpWriteEntity.FixedCost)
        {
            filter &= builder.Eq(
                "DiaVencimento",
                Convert.ToInt32(command.Values["dueDay"], CultureInfo.InvariantCulture));
            var categoryId = command.Values["categoryId"]?.ToString();
            if (!string.IsNullOrWhiteSpace(categoryId))
                filter &= IdFilter(builder, "CategoriaId", categoryId);
        }
        else
        {
            filter &= builder.Eq(
                          "Ano",
                          Convert.ToInt32(command.Values["year"], CultureInfo.InvariantCulture)) &
                      builder.Eq(
                          "Mes",
                          Convert.ToInt32(command.Values["month"], CultureInfo.InvariantCulture));
            var categoryId = command.Values["categoryId"]?.ToString();
            if (!string.IsNullOrWhiteSpace(categoryId))
                filter &= IdFilter(builder, "CategoriaId", categoryId);
        }

        var candidates = await Collection(command.Entity)
            .Find(filter)
            .ToListAsync(cancellationToken);
        return candidates.Any(candidate => MatchesDuplicate(command, candidate));
    }

    private IMongoCollection<BsonDocument> Collection(McpWriteEntity entity) =>
        _database.GetCollection<BsonDocument>(
            entity switch
            {
                McpWriteEntity.Category => "Categoria",
                McpWriteEntity.Income => "Rendimento",
                McpWriteEntity.Expense => "Despesa",
                McpWriteEntity.Investment => "Investimento",
                McpWriteEntity.FixedCost => "CustosFixos",
                _ => throw new ArgumentOutOfRangeException(nameof(entity))
            });

    private static FilterDefinition<BsonDocument> OwnedFilter(
        string id,
        string userId)
    {
        var builder = Builders<BsonDocument>.Filter;
        return IdFilter(builder, "_id", id) &
               IdFilter(builder, "UsuarioId", userId);
    }

    private static FilterDefinition<BsonDocument> IdFilter(
        FilterDefinitionBuilder<BsonDocument> builder,
        string field,
        string value) =>
        ObjectId.TryParse(value, out var id)
            ? builder.Or(
                builder.Eq(field, id),
                builder.Eq(field, value))
            : builder.Eq(field, value);

    private static McpWriteStoredRecord Map(
        McpWriteEntity entity,
        BsonDocument document)
    {
        var values = entity switch
        {
            McpWriteEntity.Category => new Dictionary<string, object?>
            {
                ["name"] = document.GetValue("Nome", "").AsString,
                ["type"] = ((TipoCategoria)document.GetValue("Tipo", 0).ToInt32()).ToString()
            },
            McpWriteEntity.FixedCost => new Dictionary<string, object?>
            {
                ["name"] = document.GetValue("Nome", "").AsString,
                ["dueDay"] = document.GetValue("DiaVencimento", 0).ToInt32(),
                ["categoryId"] = OptionalIdText(document, "CategoriaId"),
                ["active"] = document.GetValue("Ativo", true).ToBoolean()
            },
            _ => TransactionValues(entity, document)
        };
        return new McpWriteStoredRecord(
            IdText(document["_id"]),
            entity,
            values,
            OptionalText(document, "McpOperationId"),
            OptionalText(document, "LastMcpOperationId"),
            OptionalText(document, "LastMcpResultHash"));
    }

    private static Dictionary<string, object?> TransactionValues(
        McpWriteEntity entity,
        BsonDocument document)
    {
        var values = new Dictionary<string, object?>
        {
            ["year"] = document.GetValue("Ano", 0).ToInt32(),
            ["month"] = document.GetValue("Mes", 0).ToInt32(),
            ["description"] = document.GetValue("Descricao", "").AsString,
            ["amount"] = Decimal(document.GetValue("Valor", 0))
                .ToString("0.00", CultureInfo.InvariantCulture),
            ["categoryId"] = IdText(document.GetValue("CategoriaId", BsonNull.Value))
        };
        if (entity == McpWriteEntity.Expense)
        {
            values["groupingExpenseId"] = OptionalIdText(
                document,
                "IdDespesaAgrupadora");
            values["expenseOriginId"] = OptionalText(document, "DespesaOrigemId");
            values["isInstallment"] = document.GetValue("IsParcelado", false).ToBoolean();
            values["isRecurring"] = document.GetValue("IsRecorrente", false).ToBoolean();
            values["installmentNumber"] = OptionalInt(document, "ParcelaAtual");
            values["installmentCount"] = OptionalInt(document, "TotalParcelas");
        }
        return values;
    }

    private static string IdText(BsonValue value) =>
        value.IsObjectId
            ? value.AsObjectId.ToString()
            : value.ToString() ?? string.Empty;

    private static string? OptionalText(BsonDocument document, string name) =>
        document.TryGetValue(name, out var value) &&
        !value.IsBsonNull &&
        value.IsString
            ? value.AsString
            : null;

    private static string? OptionalIdText(BsonDocument document, string name) =>
        document.TryGetValue(name, out var value) && !value.IsBsonNull
            ? IdText(value)
            : null;

    private static int? OptionalInt(BsonDocument document, string name) =>
        document.TryGetValue(name, out var value) && !value.IsBsonNull
            ? value.ToInt32()
            : null;

    private static decimal Decimal(BsonValue value) =>
        value.BsonType switch
        {
            BsonType.Decimal128 => Decimal128.ToDecimal(value.AsDecimal128),
            BsonType.Double => Convert.ToDecimal(value.AsDouble, CultureInfo.InvariantCulture),
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => value.AsInt64,
            _ => decimal.Parse(
                value.ToString() ?? "0",
                CultureInfo.InvariantCulture)
        };

    private static bool MatchesDuplicate(
        McpWriteCommand command,
        BsonDocument candidate)
    {
        if (command.Entity == McpWriteEntity.Category)
        {
            return Normalized(candidate.GetValue("Nome", "").AsString) ==
                   Normalized(command.Values["name"]?.ToString());
        }
        if (command.Entity == McpWriteEntity.FixedCost)
        {
            return Normalized(candidate.GetValue("Nome", "").AsString) ==
                       Normalized(command.Values["name"]?.ToString()) &&
                   candidate.GetValue("Ativo", true).ToBoolean() ==
                       Convert.ToBoolean(
                           command.Values["active"],
                           CultureInfo.InvariantCulture);
        }

        return Decimal(candidate.GetValue("Valor", 0)) ==
                   decimal.Parse(
                       command.Values["amount"]?.ToString() ?? "0",
                       CultureInfo.InvariantCulture) &&
               Normalized(candidate.GetValue("Descricao", "").AsString) ==
                   Normalized(command.Values["description"]?.ToString());
    }

    private static string Normalized(string? value)
    {
        var decomposed = (value ?? string.Empty)
            .Trim()
            .Normalize(NormalizationForm.FormD);
        return new string(decomposed
                .Where(character =>
                    CharUnicodeInfo.GetUnicodeCategory(character) !=
                    UnicodeCategory.NonSpacingMark)
                .ToArray())
            .Normalize(NormalizationForm.FormC)
            .ToUpperInvariant();
    }
}
