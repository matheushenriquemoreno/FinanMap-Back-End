#nullable enable

using Application.DTOs;
using Domain.Enum;
using System.Text.Json.Serialization;

namespace Application.Mcp.Models;

public sealed record McpCallContext(
    string UserId,
    string ConnectionId,
    string CorrelationId,
    string ProtocolRevision = "2025-11-25",
    string ClientId = "");

public sealed record McpCategoriesInput(
    TipoCategoria? Tipo,
    string? Text,
    int Limit = 50,
    string? Cursor = null);

public sealed record McpCategoriesData(
    IReadOnlyList<ResultCategoriaDTO> Categories,
    int Count);

public sealed record McpToolEnvelope<T>(
    string SchemaVersion,
    string CorrelationId,
    string Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    T? Data,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    McpAppliedPeriod? AppliedPeriod,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    string? Currency,
    IReadOnlyDictionary<string, object?> AppliedFilters,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    McpPage? Page,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<McpToolError> Errors);

public sealed record McpAppliedPeriod(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    DateOnly? Start,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    DateOnly? End);

public sealed record McpPage(
    int Limit,
    int Count,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    string? NextCursor,
    bool HasMore = false);

public sealed record McpToolError(
    string Code,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    string? Field,
    bool Retryable,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    IReadOnlyDictionary<string, object?>? Details = null);
