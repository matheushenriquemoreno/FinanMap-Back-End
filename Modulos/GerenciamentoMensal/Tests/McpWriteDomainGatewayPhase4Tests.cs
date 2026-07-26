using Application.DTOs;
using Application.Interfaces;
using Application.Mcp.Interfaces;
using Application.Mcp.Models;
using Application.Mcp.Services;
using Domain.Enums;
using Domain.Mcp.Enums;
using Xunit;

namespace Tests;

public sealed class McpWriteDomainGatewayPhase4Tests
{
    [Fact]
    public async Task MCP_67_expense_batch_update_requires_explicit_modifier()
    {
        var effects = new EffectStoreFake();
        effects.Add(
            new McpWriteStoredRecord(
                "expense-a",
                McpWriteEntity.Expense,
                TransactionValues(
                    "expense-category",
                    ("expenseOriginId", "batch-a"),
                    ("isInstallment", true),
                    ("installmentNumber", 1),
                    ("installmentCount", 3)),
                null,
                null,
                null));
        var gateway = new McpWriteDomainGateway(
            null!, null!, null!, null!, null!, effects);

        var preparation = await gateway.PrepareAsync(
            "owner-a",
            new McpWriteCommand(
                McpWriteEntity.Expense,
                McpPreviewAction.Update,
                "expense-a",
                new Dictionary<string, object?>
                {
                    ["description"] = "Despesa alterada"
                }));

        Assert.False(preparation.IsReady);
        var error = Assert.Single(preparation.Errors);
        Assert.Equal("VALIDATION_REQUIRED", error.Code);
        Assert.Equal("batchModifier", error.Field);
    }

    [Theory]
    [InlineData(McpWriteEntity.Expense, "Despesa")]
    [InlineData(McpWriteEntity.Investment, "Investimento")]
    public async Task MCP_69_transaction_create_accepts_only_compatible_owned_category(
        McpWriteEntity entity,
        string categoryType)
    {
        var effects = new EffectStoreFake();
        effects.Add(
            new McpWriteStoredRecord(
                "category-a",
                McpWriteEntity.Category,
                new Dictionary<string, object?>
                {
                    ["name"] = "Categoria",
                    ["type"] = categoryType
                },
                null,
                null,
                null));
        var gateway = new McpWriteDomainGateway(
            null!, null!, null!, null!, null!, effects);

        var preparation = await gateway.PrepareAsync(
            "owner-a",
            new McpWriteCommand(
                entity,
                McpPreviewAction.Create,
                null,
                TransactionValues("category-a")));

        Assert.True(preparation.IsReady);
        Assert.Empty(preparation.Errors);
    }

    [Fact]
    public async Task MCP_62_fixed_cost_create_allows_absent_category_and_preserves_status()
    {
        var gateway = new McpWriteDomainGateway(
            null!,
            null!,
            null!,
            null!,
            null!,
            new EffectStoreFake());
        var command = new McpWriteCommand(
            McpWriteEntity.FixedCost,
            McpPreviewAction.Create,
            null,
            new Dictionary<string, object?>
            {
                ["name"] = "Internet",
                ["dueDay"] = 10,
                ["categoryId"] = null,
                ["active"] = true
            });

        var preparation = await gateway.PrepareAsync("owner-a", command);

        Assert.True(preparation.IsReady);
        Assert.Equal(true, preparation.ProposedValues["active"]);
    }

    [Fact]
    public async Task MCP_P4_06_installments_are_planned_as_deterministic_idempotent_steps()
    {
        var effects = new EffectStoreFake();
        effects.Add(
            new McpWriteStoredRecord(
                "category-expense",
                McpWriteEntity.Category,
                new Dictionary<string, object?>
                {
                    ["name"] = "Educação",
                    ["type"] = "Despesa"
                },
                null,
                null,
                null));
        var gateway = new McpWriteDomainGateway(
            null!, null!, null!, null!, null!, effects);
        var values = new Dictionary<string, object?>(
            TransactionValues("category-expense"))
        {
            ["amount"] = "100.00",
            ["isInstallment"] = true,
            ["isRecurring"] = false,
            ["recurrenceCount"] = 3,
            ["groupingExpenseId"] = null
        };

        var preparation = await gateway.PrepareAsync(
            "owner-a",
            new McpWriteCommand(
                McpWriteEntity.Expense,
                McpPreviewAction.Create,
                null,
                values));

        Assert.True(preparation.IsReady);
        var steps = Assert.IsAssignableFrom<IReadOnlyList<McpWriteStepPlan>>(
            preparation.Command.Steps);
        Assert.Equal(3, steps.Count);
        Assert.Equal(["expense-001", "expense-002", "expense-003"], steps.Select(x => x.Name));
        Assert.Equal(["33.33", "33.33", "33.34"], steps.Select(x => x.Values["amount"]));
        Assert.Equal([7, 8, 9], steps.Select(x => x.Values["month"]));
        Assert.All(steps, step => Assert.Null(step.ExpectedValues));
    }

    [Theory]
    [InlineData(McpPreviewAction.Update)]
    [InlineData(McpPreviewAction.Delete)]
    public async Task MCP_P4_06_batch_scope_expands_owned_targets_into_snapshot_steps(
        McpPreviewAction action)
    {
        var effects = new EffectStoreFake();
        var first = ExpenseRecord("expense-1", 1);
        var second = ExpenseRecord("expense-2", 2);
        var third = ExpenseRecord("expense-3", 3);
        effects.Add(first);
        effects.Add(second);
        effects.Add(third);
        effects.ExpenseBatch = [third, first, second];
        effects.Add(
            new McpWriteStoredRecord(
                "category-expense",
                McpWriteEntity.Category,
                new Dictionary<string, object?>
                {
                    ["name"] = "Moradia",
                    ["type"] = "Despesa"
                },
                null,
                null,
                null));
        var gateway = new McpWriteDomainGateway(
            null!, null!, null!, null!, null!, effects);
        var changes = new Dictionary<string, object?>
        {
            ["batchModifier"] = ModificadorLote.EstaEProximas.ToString()
        };
        if (action == McpPreviewAction.Update)
            changes["description"] = "Atualizada";

        var preparation = await gateway.PrepareAsync(
            "owner-a",
            new McpWriteCommand(
                McpWriteEntity.Expense,
                action,
                "expense-2",
                changes));

        Assert.True(preparation.IsReady);
        Assert.Equal(
            ["expense-2", "expense-3"],
            preparation.Command.Steps!.Select(step => step.TargetId));
        Assert.Equal(2, preparation.Snapshots.Count);
        Assert.All(
            preparation.Command.Steps!,
            step => Assert.NotNull(step.ExpectedValues));
    }

    [Fact]
    public async Task MCP_P4_06_grouping_parent_delete_expands_children_before_parent()
    {
        var effects = new EffectStoreFake();
        var parent = new McpWriteStoredRecord(
            "expense-parent",
            McpWriteEntity.Expense,
            TransactionValues(
                "category-expense",
                ("groupingExpenseId", null)),
            null,
            null,
            null);
        var secondChild = new McpWriteStoredRecord(
            "expense-child-2",
            McpWriteEntity.Expense,
            TransactionValues(
                "category-expense",
                ("groupingExpenseId", parent.Id)),
            null,
            null,
            null);
        var firstChild = new McpWriteStoredRecord(
            "expense-child-1",
            McpWriteEntity.Expense,
            TransactionValues(
                "category-expense",
                ("groupingExpenseId", parent.Id)),
            null,
            null,
            null);
        effects.Add(parent);
        effects.GroupedExpenses = [secondChild, firstChild];
        var gateway = new McpWriteDomainGateway(
            null!, null!, null!, null!, null!, effects);

        var preparation = await gateway.PrepareAsync(
            "owner-a",
            new McpWriteCommand(
                McpWriteEntity.Expense,
                McpPreviewAction.Delete,
                parent.Id,
                new Dictionary<string, object?>()));

        Assert.True(preparation.IsReady);
        Assert.Equal(
            ["expense-child-1", "expense-child-2", "expense-parent"],
            preparation.Command.Steps!.Select(step => step.TargetId));
        Assert.Equal(3, preparation.Snapshots.Count);
        Assert.All(preparation.Command.Steps!, step => Assert.Empty(step.Values));
        Assert.Equal(3, preparation.ProposedValues["affectedCount"]);
        Assert.Equal(true, preparation.ProposedValues["groupingDelete"]);
    }

    [Fact]
    public async Task MCP_56_expense_create_executes_through_application_service_with_effect_marker()
    {
        var expenses = new ExpenseServiceFake();
        var gateway = new McpWriteDomainGateway(
            null!,
            null!,
            expenses,
            null!,
            null!,
            new EffectStoreFake());
        var command = new McpWriteCommand(
            McpWriteEntity.Expense,
            McpPreviewAction.Create,
            null,
            TransactionValues(
                "category-expense",
                ("expenseOriginId", "batch-a"),
                ("isInstallment", true),
                ("isRecurring", false),
                ("installmentNumber", 1),
                ("installmentCount", 3),
                ("groupingExpenseId", null)));

        var effect = await gateway.ExecuteAsync(
            "owner-a",
            command,
            "operation-a:expense-001",
            []);

        Assert.Equal(McpDomainEffectState.Completed, effect.State);
        Assert.Equal("operation-a:expense-001", expenses.LastCreate!.McpOperationId);
        Assert.Equal("batch-a", expenses.LastCreate.DespesaOrigemId);
        Assert.Equal(1, expenses.LastCreate.ParcelaAtual);
        Assert.True(expenses.LastCreate.IsParcelado);
    }

    [Fact]
    public async Task MCP_57_expense_update_executes_conditional_mutation_through_application_service()
    {
        var expenses = new ExpenseServiceFake();
        var gateway = new McpWriteDomainGateway(
            null!,
            null!,
            expenses,
            null!,
            null!,
            new EffectStoreFake());
        var expected = TransactionValues(
            "category-expense",
            ("expenseOriginId", null),
            ("groupingExpenseId", null));
        var command = new McpWriteCommand(
            McpWriteEntity.Expense,
            McpPreviewAction.Update,
            "expense-a",
            new Dictionary<string, object?>
            {
                ["description"] = "Atualizada"
            },
            expected);

        var effect = await gateway.ExecuteAsync(
            "owner-a",
            command,
            "operation-update",
            []);

        Assert.Equal(McpDomainEffectState.Completed, effect.State);
        Assert.Equal("operation-update", expenses.LastMutationOperationId);
        Assert.Same(command, expenses.LastMutationCommand);
    }

    private static IReadOnlyDictionary<string, object?> TransactionValues(
        string categoryId,
        params (string Key, object? Value)[] extra)
    {
        var values = new Dictionary<string, object?>
        {
            ["year"] = 2026,
            ["month"] = 7,
            ["description"] = "Registro",
            ["amount"] = "100.00",
            ["categoryId"] = categoryId
        };
        foreach (var (key, value) in extra)
            values[key] = value;
        return values;
    }

    private static McpWriteStoredRecord ExpenseRecord(
        string id,
        int installmentNumber) =>
        new(
            id,
            McpWriteEntity.Expense,
            TransactionValues(
                "category-expense",
                ("expenseOriginId", "batch-a"),
                ("isInstallment", true),
                ("isRecurring", false),
                ("installmentNumber", installmentNumber),
                ("installmentCount", 3)),
            null,
            null,
            null);

    private sealed class EffectStoreFake : IMcpWriteEffectStore
    {
        private readonly Dictionary<(McpWriteEntity Entity, string Id), McpWriteStoredRecord>
            _items = [];
        public IReadOnlyList<McpWriteStoredRecord> ExpenseBatch { get; set; } = [];
        public IReadOnlyList<McpWriteStoredRecord> GroupedExpenses { get; set; } = [];

        public void Add(McpWriteStoredRecord item) =>
            _items[(item.Entity, item.Id)] = item;

        public Task<McpWriteStoredRecord?> LoadOwnedAsync(
            McpWriteEntity entity,
            string id,
            string userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.GetValueOrDefault((entity, id)));

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

        public Task<IReadOnlyList<McpWriteStoredRecord>> ListExpenseBatchAsync(
            string expenseOriginId,
            string userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ExpenseBatch);

        public Task<IReadOnlyList<McpWriteStoredRecord>> ListGroupedExpensesAsync(
            string groupingExpenseId,
            string userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(GroupedExpenses);
    }

    private sealed class ExpenseServiceFake : IDespesaService
    {
        public CreateDespesaDTO? LastCreate { get; private set; }
        public McpWriteCommand? LastMutationCommand { get; private set; }
        public string? LastMutationOperationId { get; private set; }

        public Task<Result<ResultDespesaDTO>> Adicionar(CreateDespesaDTO createDTO)
        {
            LastCreate = createDTO;
            return Task.FromResult(
                Result.Success(new ResultDespesaDTO { Id = "expense-created" }));
        }

        public Task<Result<ResultDespesaDTO>> Atualizar(UpdateDespesaDTO updateDTO) =>
            throw new NotSupportedException();

        public Task<Result> Excluir(string id) =>
            throw new NotSupportedException();

        public Task<Result<ResultDespesaDTO>> ObterPeloID(string id) =>
            throw new NotSupportedException();

        public Task<Result<ResultDespesaDTO>> AtualizarValor(
            Application.Shared.Transacao.DTOs.UpdateValorTransacaoDTO updateValorTransacaoDTO) =>
            throw new NotSupportedException();

        public Task<List<ResultDespesaDTO>> ObterMesAno(
            int mes,
            int ano,
            string descricao = null!) =>
            throw new NotSupportedException();

        public Task<List<ResultDespesaDTO>> ObterDespesasDaAgrupadora(string idDespesa) =>
            throw new NotSupportedException();

        public Task<Result> LancarDespesaEmLoteAsync(LancarDespesaLoteDTO dto) =>
            throw new NotSupportedException();

        public Task<Result> AtualizarDespesaEmLoteAsync(
            string id,
            AtualizarLoteDespesaDTO dto) =>
            throw new NotSupportedException();

        public Task<Result> ExcluirDespesaEmLoteAsync(
            string id,
            ModificadorLote modificador) =>
            throw new NotSupportedException();

        public Task<McpApplicationMutationResult> AplicarMutacaoMcpAsync(
            McpWriteCommand command,
            string operationId,
            string resultHash,
            CancellationToken cancellationToken = default)
        {
            LastMutationCommand = command;
            LastMutationOperationId = operationId;
            return Task.FromResult(
                new McpApplicationMutationResult(
                    McpApplicationMutationState.Completed,
                    command.TargetId,
                    operationId,
                    resultHash,
                    null,
                    null));
        }
    }
}
