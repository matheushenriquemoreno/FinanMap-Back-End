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

    public McpCategoriesToolService(
        ICategoriaService categories,
        IMcpConnectionValidator connections,
        IMcpOperationJournalRepository journals,
        McpAuditSanitizer sanitizer)
    {
        _categories = categories;
        _connections = connections;
        _journals = journals;
        _sanitizer = sanitizer;
    }

    public async Task<McpToolEnvelope<McpCategoriesData>> ExecuteAsync(
        McpCallContext context,
        McpCategoriesInput input,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(input.Limit, 1, 200);
        var parameters = _sanitizer.Sanitize(new Dictionary<string, object?>
        {
            ["tipo"] = input.Tipo?.ToString(),
            ["text"] = input.Text,
            ["limit"] = limit
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

            var types = input.Tipo is { } type
                ? new[] { type }
                : Enum.GetValues<TipoCategoria>();
            var categories = new List<Application.DTOs.ResultCategoriaDTO>();

            foreach (var categoryType in types)
            {
                var result = await _categories.ObterCategoria(
                    categoryType, input.Text ?? string.Empty);
                if (result.IsFailure)
                    throw new McpCategoryQueryException(result.Error?.Message ?? "Falha ao consultar categorias.");

                categories.AddRange(result.Value);
            }

            var selected = categories
                .OrderBy(item => item.Tipo)
                .ThenBy(item => item.Nome, StringComparer.CurrentCultureIgnoreCase)
                .Take(limit)
                .ToList();
            var status = selected.Count == 0 ? "empty" : "success";
            var response = new McpToolEnvelope<McpCategoriesData>(
                McpProtocolContract.SchemaVersion,
                context.CorrelationId,
                status,
                new McpCategoriesData(selected, selected.Count),
                null,
                null,
                parameters,
                new McpPage(limit, selected.Count, null),
                [],
                []);

            journal.Complete(new Dictionary<string, object?>
            {
                ["status"] = status,
                ["count"] = selected.Count
            });
            await _journals.CompleteAsync(journal, journal.ResultSummary, cancellationToken);
            return response;
        }
        catch
        {
            journal.Fail("CATEGORY_QUERY_FAILED");
            await _journals.FailAsync(journal, "CATEGORY_QUERY_FAILED", cancellationToken);
            throw;
        }
    }
}

public sealed class McpCategoryQueryException(string message) : Exception(message);
