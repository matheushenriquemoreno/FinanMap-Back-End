using System.Text.Json;
using System.Globalization;
using Application.DTOs;
using Application.Interfaces;
using Application.Mcp.Interfaces;
using Application.Mcp.Models;
using Application.Mcp.Services;
using Domain.Entity;
using Domain.Enum;
using Domain.Mcp.Entities;
using Domain.Mcp.Repositories;
using Xunit;

namespace Tests;

public class McpCategoriesToolServiceTests
{
    [Fact]
    public async Task Lists_only_categories_returned_by_existing_service_after_journal_is_persisted()
    {
        var startedAfter = DateTime.UtcNow.AddSeconds(-1);
        var events = new List<string>();
        var categories = new CategoriaServiceFake(events,
            new ResultCategoriaDTO { Id = "cat-a", Nome = "Mercado", Tipo = TipoCategoria.Despesa });
        var journals = new JournalFake(events);
        var service = new McpCategoriesToolService(
            categories,
            new ConnectionValidatorFake(events),
            journals,
            new McpAuditSanitizer(),
            CursorCodec());

        var response = await service.ExecuteAsync(
            new McpCallContext("owner-a", "connection-a", "correlation-a", ClientId: "client-a"),
            new McpCategoriesInput(TipoCategoria.Despesa, " mer ", 50, null));

        Assert.Equal("success", response.Status);
        Assert.Single(response.Data!.Categories);
        Assert.Equal("cat-a", response.Data.Categories[0].Id);
        Assert.Equal("mer", categories.LastDescription);
        Assert.Equal("client-a", journals.LastAdded!.Origin["clientId"]);
        Assert.Equal("2025-11-25", journals.LastAdded.Origin["protocolRevision"]);
        Assert.Equal(["journal", "connection-validated", "category-service", "journal-complete"], events);
        Assert.Equal("owner-a", journals.LastAdded.UserId);
        Assert.Equal("connection-a", journals.LastAdded.ConnectionId);
        Assert.Equal("finanmap_categories_list", journals.LastAdded.ToolName);
        Assert.Equal(2, response.AppliedFilters.Count);
        Assert.Equal("Despesa", response.AppliedFilters["tipo"]);
        Assert.Equal("mer", response.AppliedFilters["text"]);
        Assert.Equal("Despesa", journals.LastAdded.SanitizedParameters["tipo"]);
        Assert.Equal(true, journals.LastAdded.SanitizedParameters["textFilter"]);
        Assert.Equal(false, journals.LastAdded.SanitizedParameters["cursorPresent"]);
        Assert.False(journals.LastAdded.SanitizedParameters.ContainsKey("text"));
        Assert.Equal(50, journals.LastAdded.SanitizedParameters["limit"]);
        Assert.True(journals.LastAdded.StartedAtUtc >= startedAfter);
        Assert.NotNull(journals.LastAdded.FinishedAtUtc);
        Assert.Equal("success", journals.LastAdded.ResultSummary["status"]);
        Assert.Equal(1, journals.LastAdded.ResultSummary["count"]);

        var responseJson = JsonSerializer.Serialize(response);
        Assert.DoesNotContain("UsuarioId", responseJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("access_token", responseJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authorization", responseJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Journal_failure_prevents_category_service_execution()
    {
        var events = new List<string>();
        var service = new McpCategoriesToolService(
            new CategoriaServiceFake(events),
            new ConnectionValidatorFake(events),
            new JournalFake(events, failOnCreate: true),
            new McpAuditSanitizer(),
            CursorCodec());

        await Assert.ThrowsAsync<McpJournalUnavailableException>(() =>
            service.ExecuteAsync(
                new McpCallContext("owner-a", "connection-a", "correlation-a"),
                new McpCategoriesInput(null, null, 50, null)));

        Assert.Equal(["journal"], events);
    }

    [Fact]
    public async Task Categories_cursor_is_signed_owner_and_filter_bound_with_no_raw_query_in_audit()
    {
        var events = new List<string>();
        var categories = new CategoriaServiceFake(
            events,
            new ResultCategoriaDTO
            {
                Id = "cat-a",
                Nome = "Alimentação",
                Tipo = TipoCategoria.Despesa
            },
            new ResultCategoriaDTO
            {
                Id = "cat-b",
                Nome = "Moradia",
                Tipo = TipoCategoria.Despesa
            });
        var journals = new JournalFake(events);
        var service = new McpCategoriesToolService(
            categories,
            new ConnectionValidatorFake(events),
            journals,
            new McpAuditSanitizer(),
            CursorCodec());
        var ownerA = new McpCallContext(
            "owner-a", "connection-a", "correlation-a");

        var first = await service.ExecuteAsync(
            ownerA,
            new McpCategoriesInput(
                TipoCategoria.Despesa, "consulta-sensivel", 1, null));
        var second = await service.ExecuteAsync(
            ownerA,
            new McpCategoriesInput(
                TipoCategoria.Despesa,
                "consulta-sensivel",
                1,
                first.Page!.NextCursor));
        var crossOwner = await service.ExecuteAsync(
            new McpCallContext("owner-b", "connection-b", "correlation-b"),
            new McpCategoriesInput(
                TipoCategoria.Despesa,
                "consulta-sensivel",
                1,
                first.Page.NextCursor));
        var changedFilter = await service.ExecuteAsync(
            ownerA,
            new McpCategoriesInput(
                TipoCategoria.Despesa,
                "outra-consulta",
                1,
                first.Page.NextCursor));

        Assert.True(first.Page.HasMore);
        Assert.NotNull(first.Page.NextCursor);
        Assert.False(second.Page!.HasMore);
        Assert.Null(second.Page.NextCursor);
        Assert.NotEqual(
            Assert.Single(first.Data!.Categories).Id,
            Assert.Single(second.Data!.Categories).Id);
        Assert.Equal("INVALID_CURSOR", Assert.Single(crossOwner.Errors).Code);
        Assert.Equal("INVALID_CURSOR", Assert.Single(changedFilter.Errors).Code);
        Assert.Equal(true, journals.LastAdded!.SanitizedParameters["textFilter"]);
        Assert.False(journals.LastAdded.SanitizedParameters.ContainsKey("text"));
        Assert.False(journals.LastAdded.SanitizedParameters.ContainsKey("cursor"));
    }

    [Fact]
    public async Task Categories_continuation_detects_changed_snapshot()
    {
        var events = new List<string>();
        var values = new[]
        {
            new ResultCategoriaDTO
            {
                Id = "cat-a",
                Nome = "Alimentação",
                Tipo = TipoCategoria.Despesa
            },
            new ResultCategoriaDTO
            {
                Id = "cat-b",
                Nome = "Moradia",
                Tipo = TipoCategoria.Despesa
            }
        };
        var service = new McpCategoriesToolService(
            new CategoriaServiceFake(events, values),
            new ConnectionValidatorFake(events),
            new JournalFake(events),
            new McpAuditSanitizer(),
            CursorCodec());
        var context = new McpCallContext(
            "owner-a", "connection-a", "correlation-conflict");

        var first = await service.ExecuteAsync(
            context,
            new McpCategoriesInput(
                TipoCategoria.Despesa, null, 1, null));
        values[1].Nome = "Antes de alimentação";
        var continuation = await service.ExecuteAsync(
            context,
            new McpCategoriesInput(
                TipoCategoria.Despesa, null, 1, first.Page!.NextCursor));

        Assert.Equal(
            "CONFLICT_CHANGED",
            Assert.Single(continuation.Errors).Code);
    }

    [Fact]
    public async Task Categories_pagination_order_is_identical_under_en_us_and_tr_tr()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var enUs = await ReadAllCategoryIdsAsync(new CultureInfo("en-US"));
            var trTr = await ReadAllCategoryIdsAsync(new CultureInfo("tr-TR"));

            Assert.Equal(enUs, trTr);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private static async Task<string[]> ReadAllCategoryIdsAsync(CultureInfo culture)
    {
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        var events = new List<string>();
        var categories = new CategoriaServiceFake(
            events,
            new ResultCategoriaDTO
            {
                Id = "cat-uppercase-i",
                Nome = "I",
                Tipo = TipoCategoria.Despesa
            },
            new ResultCategoriaDTO
            {
                Id = "cat-lowercase-i",
                Nome = "i",
                Tipo = TipoCategoria.Despesa
            },
            new ResultCategoriaDTO
            {
                Id = "cat-dotted-i",
                Nome = "İ",
                Tipo = TipoCategoria.Despesa
            },
            new ResultCategoriaDTO
            {
                Id = "cat-dotless-i",
                Nome = "ı",
                Tipo = TipoCategoria.Despesa
            });
        var service = new McpCategoriesToolService(
            categories,
            new ConnectionValidatorFake(events),
            new JournalFake(events),
            new McpAuditSanitizer(),
            CursorCodec());
        var context = new McpCallContext(
            "owner-a", "connection-a", $"correlation-{culture.Name}");
        var ids = new List<string>();
        string? cursor = null;
        do
        {
            var page = await service.ExecuteAsync(
                context,
                new McpCategoriesInput(
                    TipoCategoria.Despesa, null, 1, cursor));
            ids.AddRange(page.Data!.Categories.Select(item => item.Id));
            cursor = page.Page!.NextCursor;
        }
        while (cursor is not null);

        return ids.ToArray();
    }

    [Fact]
    public void Sanitizer_uses_allowlist_and_redacts_secret_like_values()
    {
        var sanitized = new McpAuditSanitizer().Sanitize(new Dictionary<string, object?>
        {
            ["tipo"] = "Despesa",
            ["textFilter"] = true,
            ["limit"] = 50,
            ["text"] = "never-store-this",
            ["cursor"] = "never-store-this",
            ["access_token"] = "never-store-this"
        });

        Assert.Equal("Despesa", sanitized["tipo"]);
        Assert.Equal(true, sanitized["textFilter"]);
        Assert.Equal(50, sanitized["limit"]);
        Assert.False(sanitized.ContainsKey("text"));
        Assert.False(sanitized.ContainsKey("cursor"));
        Assert.False(sanitized.ContainsKey("access_token"));
    }

    private static McpCursorCodec CursorCodec() =>
        new("categories-test-cursor-signing-key-32-bytes"u8.ToArray());

    private sealed class ConnectionValidatorFake(List<string> events) : IMcpConnectionValidator
    {
        public Task<McpConnection> ValidateActiveAsync(
            string connectionId, string userId, string requiredScope,
            CancellationToken cancellationToken = default)
        {
            events.Add("connection-validated");
            return Task.FromResult(McpConnection.CreateActive(
                userId, "authorization-a", "client-a", "Cliente", [requiredScope]));
        }
    }

    private sealed class JournalFake(List<string> events, bool failOnCreate = false)
        : IMcpOperationJournalRepository
    {
        public McpOperationJournal? LastAdded { get; private set; }

        public Task AddAsync(McpOperationJournal journal, CancellationToken cancellationToken = default)
        {
            events.Add("journal");
            LastAdded = journal;
            if (failOnCreate)
                throw new InvalidOperationException("journal indisponível");
            return Task.CompletedTask;
        }

        public Task CompleteAsync(
            McpOperationJournal journal, object? resultSummary, CancellationToken cancellationToken = default)
        {
            events.Add("journal-complete");
            return Task.CompletedTask;
        }

        public Task FailAsync(
            McpOperationJournal journal, string errorCode, CancellationToken cancellationToken = default)
        {
            events.Add("journal-failed");
            return Task.CompletedTask;
        }
    }

    private sealed class CategoriaServiceFake(List<string> events, params ResultCategoriaDTO[] categories)
        : ICategoriaService
    {
        public string? LastDescription { get; private set; }

        public Task<Result<List<ResultCategoriaDTO>>> ObterCategoria(TipoCategoria tipoCategoria, string descricao)
        {
            events.Add("category-service");
            LastDescription = descricao;
            return Task.FromResult(Result.Success(categories.Where(x => x.Tipo == tipoCategoria).ToList()));
        }

        public Task<Result<ResultCategoriaDTO>> Adicionar(CreateCategoriaDTO createDTO) =>
            throw new NotSupportedException();

        public Task<Result<ResultCategoriaDTO>> Atualizar(UpdateCategoriaDTO updateDTO) =>
            throw new NotSupportedException();

        public Task<Result> Excluir(string id) => throw new NotSupportedException();

        public Task<Result<ResultCategoriaDTO>> ObterPeloID(string id) =>
            throw new NotSupportedException();
    }
}
