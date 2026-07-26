using Application.DTOs;
using Application.Interface;
using Application.Interfaces;
using Application.Mcp.Interfaces;
using Application.Mcp.Models;
using Application.Mcp.Services;
using Application.Shared.Transacao.DTOs;
using Domain.Entity;
using Domain.Enum;
using Domain.Mcp.Enums;
using Xunit;

namespace Tests;

public sealed class McpWriteDomainGatewayPhase3Tests
{
    [Fact]
    public async Task Prepare_is_owner_scoped_validates_relationships_and_builds_immutable_snapshot()
    {
        var store = new EffectStoreFake
        {
            Record = new McpWriteStoredRecord(
                "income-a",
                McpWriteEntity.Income,
                new Dictionary<string, object?>
                {
                    ["year"] = 2026,
                    ["month"] = 7,
                    ["description"] = "Antes",
                    ["amount"] = "100.00",
                    ["categoryId"] = "category-income"
                },
                null,
                null,
                null),
            Category = new McpWriteStoredRecord(
                "category-income",
                McpWriteEntity.Category,
                new Dictionary<string, object?>
                {
                    ["name"] = "Receitas",
                    ["type"] = "Rendimento"
                },
                null,
                null,
                null)
        };
        var gateway = new McpWriteDomainGateway(
            new CategoriaServiceFake(),
            new RendimentoServiceFake(),
            store);
        var command = new McpWriteCommand(
            McpWriteEntity.Income,
            McpPreviewAction.Update,
            "income-a",
            new Dictionary<string, object?>
            {
                ["amount"] = "125.50",
                ["categoryId"] = "category-income"
            });

        var prepared = await gateway.PrepareAsync("owner-a", command);

        Assert.True(prepared.IsReady);
        Assert.Equal("100.00", prepared.CurrentValues!["amount"]);
        Assert.Equal("125.50", prepared.ProposedValues["amount"]);
        Assert.Equal(
            "100.00",
            prepared.Command.ExpectedValues!["amount"]);
        Assert.Single(prepared.Snapshots);
        Assert.Equal("owner-a", store.LastOwner);

        store.Category = new McpWriteStoredRecord(
            "category-expense",
            McpWriteEntity.Category,
            new Dictionary<string, object?>
            {
                ["name"] = "Despesa",
                ["type"] = "Despesa"
            },
            null,
            null,
            null);
        var invalidRelationship = await gateway.PrepareAsync(
            "owner-a",
            command with
            {
                Values = new Dictionary<string, object?>
                {
                    ["categoryId"] = "category-expense"
                }
            });
        Assert.Equal(
            "CATEGORY_RELATIONSHIP_INVALID",
            Assert.Single(invalidRelationship.Errors).Code);

        store.Record = null;
        var crossAccountEquivalent = await gateway.PrepareAsync(
            "owner-a",
            command);
        Assert.Equal(
            "RECORD_NOT_FOUND",
            Assert.Single(crossAccountEquivalent.Errors).Code);
    }

    [Fact]
    public async Task Execute_uses_service_for_create_effect_marker_and_conditional_store_for_update_delete()
    {
        var categoryService = new CategoriaServiceFake();
        var store = new EffectStoreFake
        {
            Record = new McpWriteStoredRecord(
                "category-a",
                McpWriteEntity.Category,
                new Dictionary<string, object?>
                {
                    ["name"] = "Antes",
                    ["type"] = "Despesa"
                },
                null,
                null,
                null),
        };
        var gateway = new McpWriteDomainGateway(
            categoryService,
            new RendimentoServiceFake(),
            store);
        var create = new McpWriteCommand(
            McpWriteEntity.Category,
            McpPreviewAction.Create,
            null,
            new Dictionary<string, object?>
            {
                ["name"] = "Nova",
                ["type"] = "Despesa"
            });

        var created = await gateway.ExecuteAsync(
            "owner-a",
            create,
            "operation-create",
            []);

        Assert.Equal(McpDomainEffectState.Completed, created.State);
        Assert.Equal("operation-create", categoryService.LastCreate!.McpOperationId);

        var update = new McpWriteCommand(
            McpWriteEntity.Category,
            McpPreviewAction.Update,
            "category-a",
            new Dictionary<string, object?> { ["name"] = "Depois" },
            new Dictionary<string, object?>
            {
                ["name"] = "Antes",
                ["type"] = "Despesa"
            });
        var updated = await gateway.ExecuteAsync(
            "owner-a",
            update,
            "operation-update",
            []);
        Assert.Equal(McpDomainEffectState.Completed, updated.State);
        Assert.Equal("operation-update", categoryService.LastMutationOperationId);
        Assert.Equal(McpPreviewAction.Update, categoryService.LastMutationCommand!.Action);

        categoryService.MutationResult = new McpApplicationMutationResult(
            McpApplicationMutationState.Unknown,
            "category-a",
            null,
            null,
            "EFFECT_OUTCOME_UNKNOWN",
            "Causalidade desconhecida.");
        var deletedUnknown = await gateway.ExecuteAsync(
            "owner-a",
            update with { Action = McpPreviewAction.Delete },
            "operation-delete",
            []);
        Assert.Equal(McpDomainEffectState.Unknown, deletedUnknown.State);
        Assert.Equal(McpPreviewAction.Delete, categoryService.LastMutationCommand.Action);
    }

    private sealed class EffectStoreFake : IMcpWriteEffectStore
    {
        public McpWriteStoredRecord? Record { get; set; }
        public McpWriteStoredRecord? Category { get; set; }
        public string? LastOwner { get; private set; }

        public Task<McpWriteStoredRecord?> LoadOwnedAsync(
            McpWriteEntity entity,
            string id,
            string userId,
            CancellationToken cancellationToken = default)
        {
            LastOwner = userId;
            if (entity == McpWriteEntity.Category &&
                Category?.Id == id)
            {
                return Task.FromResult<McpWriteStoredRecord?>(Category);
            }
            return Task.FromResult(Record?.Id == id ? Record : null);
        }

        public Task<bool> CategoryHasLinksAsync(
            string id,
            string userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<McpWriteStoredRecord?> FindEffectAsync(
            McpWriteEntity entity,
            string userId,
            string operationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<McpWriteStoredRecord?>(null);
    }

    private sealed class CategoriaServiceFake : ICategoriaService
    {
        public CreateCategoriaDTO? LastCreate { get; private set; }
        public McpWriteCommand? LastMutationCommand { get; private set; }
        public string? LastMutationOperationId { get; private set; }
        public McpApplicationMutationResult MutationResult { get; set; } =
            new(
                McpApplicationMutationState.Completed,
                "category-a",
                "operation-update",
                "result-hash",
                null,
                null);

        public Task<Result<ResultCategoriaDTO>> Adicionar(CreateCategoriaDTO createDTO)
        {
            LastCreate = createDTO;
            return Task.FromResult(Result.Success(new ResultCategoriaDTO
            {
                Id = "category-created",
                Nome = createDTO.Nome,
                Tipo = createDTO.Tipo!.Value
            }));
        }

        public Task<Result<List<ResultCategoriaDTO>>> ObterCategoria(
            TipoCategoria tipoCategoria,
            string descricao) =>
            Task.FromResult(Result.Success(new List<ResultCategoriaDTO>()));

        public Task<Result<ResultCategoriaDTO>> Atualizar(UpdateCategoriaDTO updateDTO) =>
            throw new NotSupportedException();

        public Task<Result> Excluir(string id) => throw new NotSupportedException();

        public Task<Result<ResultCategoriaDTO>> ObterPeloID(string id) =>
            throw new NotSupportedException();

        public Task<McpApplicationMutationResult> AplicarMutacaoMcpAsync(
            McpWriteCommand command,
            string operationId,
            string resultHash,
            CancellationToken cancellationToken = default)
        {
            LastMutationCommand = command;
            LastMutationOperationId = operationId;
            return Task.FromResult(MutationResult with
            {
                EffectMarker = MutationResult.State ==
                               McpApplicationMutationState.Completed
                    ? operationId
                    : MutationResult.EffectMarker,
                ResultHash = resultHash
            });
        }
    }

    private sealed class RendimentoServiceFake : IRendimentoService
    {
        public Task<Result<ResultRendimentoDTO>> Adicionar(CreateRendimentoDTO createDTO) =>
            Task.FromResult(Result.Success(new ResultRendimentoDTO
            {
                Id = "income-created",
                Ano = createDTO.Ano,
                Mes = createDTO.Mes,
                Descricao = createDTO.Descricao,
                Valor = createDTO.Valor,
                CategoriaId = createDTO.CategoriaId
            }));

        public Task<Result<ResultRendimentoDTO>> Atualizar(UpdateRendimentoDTO updateDTO) =>
            throw new NotSupportedException();

        public Task<Result> Excluir(string id) => throw new NotSupportedException();

        public Task<Result<ResultRendimentoDTO>> ObterPeloID(string id) =>
            throw new NotSupportedException();

        public Task<Result<ResultRendimentoDTO>> AtualizarValor(
            UpdateValorTransacaoDTO updateValorTransacaoDTO) =>
            throw new NotSupportedException();

        public Task<List<ResultRendimentoDTO>> ObterRendimentoMes(int mes, int ano) =>
            throw new NotSupportedException();
    }
}
