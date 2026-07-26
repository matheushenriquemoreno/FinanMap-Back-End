#nullable enable

using System.Globalization;
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

    private IMongoCollection<BsonDocument> Collection(McpWriteEntity entity) =>
        _database.GetCollection<BsonDocument>(
            entity == McpWriteEntity.Category ? "Categoria" : "Rendimento");

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
        var values = entity == McpWriteEntity.Category
            ? new Dictionary<string, object?>
            {
                ["name"] = document.GetValue("Nome", "").AsString,
                ["type"] = ((TipoCategoria)document.GetValue("Tipo", 0).ToInt32()).ToString()
            }
            : new Dictionary<string, object?>
            {
                ["year"] = document.GetValue("Ano", 0).ToInt32(),
                ["month"] = document.GetValue("Mes", 0).ToInt32(),
                ["description"] = document.GetValue("Descricao", "").AsString,
                ["amount"] = Decimal(document.GetValue("Valor", 0))
                    .ToString("0.00", CultureInfo.InvariantCulture),
                ["categoryId"] = IdText(document.GetValue("CategoriaId", BsonNull.Value))
            };
        return new McpWriteStoredRecord(
            IdText(document["_id"]),
            entity,
            values,
            OptionalText(document, "McpOperationId"),
            OptionalText(document, "LastMcpOperationId"),
            OptionalText(document, "LastMcpResultHash"));
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
}
