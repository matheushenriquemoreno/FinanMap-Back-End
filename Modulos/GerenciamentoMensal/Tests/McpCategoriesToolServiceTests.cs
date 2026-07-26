using System.Text.Json;
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
            categories, new ConnectionValidatorFake(events), journals, new McpAuditSanitizer());

        var response = await service.ExecuteAsync(
            new McpCallContext("owner-a", "connection-a", "correlation-a", ClientId: "client-a"),
            new McpCategoriesInput(TipoCategoria.Despesa, "mer", 50));

        Assert.Equal("success", response.Status);
        Assert.Single(response.Data!.Categories);
        Assert.Equal("cat-a", response.Data.Categories[0].Id);
        Assert.Equal("client-a", journals.LastAdded!.Origin["clientId"]);
        Assert.Equal("2025-11-25", journals.LastAdded.Origin["protocolRevision"]);
        Assert.Equal(["journal", "connection-validated", "category-service", "journal-complete"], events);
        Assert.Equal("owner-a", journals.LastAdded.UserId);
        Assert.Equal("connection-a", journals.LastAdded.ConnectionId);
        Assert.Equal("finanmap_categories_list", journals.LastAdded.ToolName);
        Assert.Equal("Despesa", journals.LastAdded.SanitizedParameters["tipo"]);
        Assert.Equal("mer", journals.LastAdded.SanitizedParameters["text"]);
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
            new McpAuditSanitizer());

        await Assert.ThrowsAsync<McpJournalUnavailableException>(() =>
            service.ExecuteAsync(
                new McpCallContext("owner-a", "connection-a", "correlation-a"),
                new McpCategoriesInput(null, null, 50)));

        Assert.Equal(["journal"], events);
    }

    [Fact]
    public void Sanitizer_uses_allowlist_and_redacts_secret_like_values()
    {
        var sanitized = new McpAuditSanitizer().Sanitize(new Dictionary<string, object?>
        {
            ["tipo"] = "Despesa",
            ["text"] = "Bearer secret-token-value",
            ["limit"] = 50,
            ["access_token"] = "never-store-this"
        });

        Assert.Equal("Despesa", sanitized["tipo"]);
        Assert.Equal("[REDACTED]", sanitized["text"]);
        Assert.Equal(50, sanitized["limit"]);
        Assert.False(sanitized.ContainsKey("access_token"));
    }

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
        public Task<Result<List<ResultCategoriaDTO>>> ObterCategoria(TipoCategoria tipoCategoria, string descricao)
        {
            events.Add("category-service");
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
