#nullable enable

using Application.Interfaces;
using Application.Mcp.Configuration;
using Application.Mcp.Interfaces;
using Application.Mcp.Models;
using Domain.Enum;
using Domain.Mcp.Entities;
using Domain.Mcp.Enums;
using Domain.Mcp.Repositories;

namespace Application.Mcp.Services;

public sealed class McpCategoriesToolService
{
    private const string ToolName = "finanmap_categories_list";
    private readonly ICategoriaService _categories;
    private readonly IMcpConnectionValidator _connections;
    private readonly IMcpOperationJournalRepository _journals;
    private readonly McpAuditSanitizer _sanitizer;
    private readonly McpCursorCodec _cursors;

    public McpCategoriesToolService(
        ICategoriaService categories,
        IMcpConnectionValidator connections,
        IMcpOperationJournalRepository journals,
        McpAuditSanitizer sanitizer,
        McpCursorCodec cursors)
    {
        _categories = categories;
        _connections = connections;
        _journals = journals;
        _sanitizer = sanitizer;
        _cursors = cursors;
    }

    public async Task<McpToolEnvelope<McpCategoriesData>> ExecuteAsync(
        McpCallContext context,
        McpCategoriesInput input,
        CancellationToken cancellationToken = default)
    {
        var limit = input.Limit == 0 ? 50 : input.Limit;
        var normalizedText = string.IsNullOrWhiteSpace(input.Text)
            ? null
            : input.Text.Trim();
        var parameters = _sanitizer.Sanitize(new Dictionary<string, object?>
        {
            ["tipo"] = input.Tipo?.ToString(),
            ["textFilter"] = normalizedText is not null,
            ["limit"] = limit,
            ["cursorPresent"] = !string.IsNullOrWhiteSpace(input.Cursor)
        });
        var journal = McpOperationJournal.Start(
            context.UserId,
            context.ConnectionId,
            context.CorrelationId,
            ToolName,
            McpOperationClass.Read,
            parameters,
            new Dictionary<string, object?>
            {
                ["clientId"] = context.ClientId,
                ["protocolRevision"] = context.ProtocolRevision,
                ["channel"] = "mcp"
            });

        try
        {
            await _journals.AddAsync(journal, cancellationToken);
        }
        catch (Exception exception)
        {
            throw new McpJournalUnavailableException(exception);
        }

        try
        {
            await _connections.ValidateActiveAsync(
                context.ConnectionId, context.UserId, "mcp:read", cancellationToken);

            if (limit is < 1 or > 200)
            {
                return await CompleteAsync(
                    journal,
                    Rejected(
                        context,
                        "LIMIT_EXCEEDED",
                        "O limite deve estar entre 1 e 200; reduza o valor solicitado.",
                        "limit"),
                    cancellationToken);
            }

            var fingerprint = McpCursorCodec.CanonicalFingerprint(new
            {
                tipo = input.Tipo?.ToString(),
                text = normalizedText,
                limit
            });
            if (!_cursors.TryDecode(
                    input.Cursor,
                    context.UserId,
                    ToolName,
                    fingerprint,
                    out var offset,
                    out var cursorSnapshot))
            {
                return await CompleteAsync(
                    journal,
                    Rejected(
                        context,
                        "INVALID_CURSOR",
                        "O cursor não é válido para esta conta, ferramenta e filtros.",
                        "cursor"),
                    cancellationToken);
            }

            var types = input.Tipo is { } type
                ? new[] { type }
                : Enum.GetValues<TipoCategoria>();
            var categories = new List<Application.DTOs.ResultCategoriaDTO>();

            foreach (var categoryType in types)
            {
                var result = await _categories.ObterCategoria(
                    categoryType, normalizedText ?? string.Empty);
                if (result.IsFailure)
                    throw new McpCategoryQueryException(result.Error?.Message ?? "Falha ao consultar categorias.");

                categories.AddRange(result.Value);
            }

            var ordered = categories
                .OrderBy(item => item.Tipo)
                .ThenBy(item => item.Nome, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray();
            var snapshot = McpCursorCodec.CanonicalFingerprint(
                ordered.Select(item => new
                {
                    id = item.Id,
                    name = item.Nome,
                    type = item.Tipo.ToString()
                }).ToArray());
            if (cursorSnapshot is not null &&
                !string.Equals(cursorSnapshot, snapshot, StringComparison.Ordinal))
            {
                return await CompleteAsync(
                    journal,
                    Rejected(
                        context,
                        "CONFLICT_CHANGED",
                        "As categorias mudaram desde a página anterior; reinicie a consulta.",
                        "cursor"),
                    cancellationToken);
            }
            if (offset > ordered.Length)
            {
                return await CompleteAsync(
                    journal,
                    Rejected(
                        context,
                        "INVALID_CURSOR",
                        "O cursor não é mais válido; reinicie a consulta.",
                        "cursor"),
                    cancellationToken);
            }

            var selected = ordered.Skip(offset).Take(limit).ToList();
            var hasMore = offset + selected.Count < ordered.Length;
            var nextCursor = hasMore
                ? _cursors.Encode(
                    offset + selected.Count,
                    context.UserId,
                    ToolName,
                    fingerprint,
                    snapshot)
                : null;
            var status = selected.Count == 0 ? "empty" : "success";
            var response = new McpToolEnvelope<McpCategoriesData>(
                McpProtocolContract.SchemaVersion,
                context.CorrelationId,
                status,
                new McpCategoriesData(selected, selected.Count),
                null,
                null,
                new Dictionary<string, object?>
                {
                    ["tipo"] = input.Tipo?.ToString(),
                    ["text"] = normalizedText
                },
                new McpPage(limit, selected.Count, nextCursor, hasMore),
                [],
                []);

            return await CompleteAsync(journal, response, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            const string code = "QUERY_CANCELLED";
            journal.Fail(code);
            await _journals.FailAsync(journal, code, cancellationToken);
            throw;
        }
        catch (McpConnectionNotFoundException)
        {
            return await FailAndRejectAsync(
                journal,
                context,
                "AUTH_CONNECTION_INVALID",
                "A conexão MCP não é válida para esta conta.",
                cancellationToken);
        }
        catch (McpConnectionInactiveException)
        {
            return await FailAndRejectAsync(
                journal,
                context,
                "AUTH_CONNECTION_INACTIVE",
                "A conexão MCP está inativa ou expirada; reconecte a integração.",
                cancellationToken);
        }
        catch (McpScopeMissingException)
        {
            return await FailAndRejectAsync(
                journal,
                context,
                "AUTH_SCOPE_MISSING",
                "A conexão MCP não possui o escopo mcp:read necessário.",
                cancellationToken);
        }
        catch
        {
            return await FailAndRejectAsync(
                journal,
                context,
                "QUERY_UNAVAILABLE",
                "A consulta de categorias está temporariamente indisponível; tente novamente.",
                cancellationToken,
                retryable: true);
        }
    }

    private async Task<McpToolEnvelope<McpCategoriesData>> CompleteAsync(
        McpOperationJournal journal,
        McpToolEnvelope<McpCategoriesData> response,
        CancellationToken cancellationToken)
    {
        journal.Complete(new Dictionary<string, object?>
        {
            ["status"] = response.Status,
            ["count"] = response.Data?.Count ?? 0
        });
        await _journals.CompleteAsync(journal, journal.ResultSummary, cancellationToken);
        return response;
    }

    private async Task<McpToolEnvelope<McpCategoriesData>> FailAndRejectAsync(
        McpOperationJournal journal,
        McpCallContext context,
        string code,
        string message,
        CancellationToken cancellationToken,
        bool retryable = false)
    {
        journal.Fail(code);
        await _journals.FailAsync(journal, code, cancellationToken);
        return Rejected(context, code, message, null, retryable);
    }

    private static McpToolEnvelope<McpCategoriesData> Rejected(
        McpCallContext context,
        string code,
        string message,
        string? field,
        bool retryable = false) =>
        new(
            McpProtocolContract.SchemaVersion,
            context.CorrelationId,
            "rejected",
            null,
            null,
            null,
            new Dictionary<string, object?>(),
            null,
            [],
            [new McpToolError(code, message, field, retryable)]);
}

public sealed class McpCategoryQueryException(string message) : Exception(message);
