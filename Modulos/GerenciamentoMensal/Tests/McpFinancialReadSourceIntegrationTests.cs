using Application.Mcp.Models;
using Application.Mcp.Services;
using Domain.Entity;
using Domain.Enum;
using Infra.Data.Mongo.Repositorys;
using MongoDB.Driver;
using Xunit;

namespace Tests;

[Collection(McpMongoCollection.Name)]
public sealed class McpFinancialReadSourceIntegrationTests(McpMongoFixture mongo)
{
    [Fact]
    public async Task Mongo_source_isolates_owners_avoids_grouped_expense_double_count_and_minimizes_cross_owner_categories()
    {
        var client = new MongoClient(mongo.ConnectionString);
        var categories = new CategoriaRepository(client);
        var incomes = new RendimentoRepository(client, categories);
        var expenses = new DespesaRepository(client, categories);
        var investments = new InvestimentoRepository(client, categories);
        var fixedCosts = new CustoFixoRepository(client);
        var source = new McpFinancialReadSource(
            incomes, expenses, investments, fixedCosts, categories);
        var suffix = Guid.NewGuid().ToString("N");
        var ownerA = $"owner-a-{suffix}";
        var ownerB = $"owner-b-{suffix}";
        var userA = new Usuario("Owner A", $"owner-a-{suffix}@example.test")
        {
            Id = ownerA
        };
        var userB = new Usuario("Owner B", $"owner-b-{suffix}@example.test")
        {
            Id = ownerB
        };
        var incomeCategoryA = new Categoria(
            "Receita A", TipoCategoria.Rendimento, ownerA)
        {
            Id = $"income-category-a-{suffix}"
        };
        var incomeCategoryB = new Categoria(
            "Receita B", TipoCategoria.Rendimento, ownerB)
        {
            Id = $"income-category-b-{suffix}"
        };
        var expenseCategoryA = new Categoria(
            "Despesa A", TipoCategoria.Despesa, ownerA)
        {
            Id = $"expense-category-a-{suffix}"
        };
        var investmentCategoryA = new Categoria(
            "Investimento A", TipoCategoria.Investimento, ownerA)
        {
            Id = $"investment-category-a-{suffix}"
        };
        var investmentCategoryB = new Categoria(
            "Investimento B", TipoCategoria.Investimento, ownerB)
        {
            Id = $"investment-category-b-{suffix}"
        };
        var incomeA = new Rendimento(
            2026, 1, "Receita da conta A", 100m, incomeCategoryA, userA)
        {
            Id = $"income-a-{suffix}"
        };
        var incomeB = new Rendimento(
            2026, 1, "Receita da conta B", 200m, incomeCategoryB, userB)
        {
            Id = $"income-b-{suffix}"
        };
        var crossCategoryIncome = new Rendimento(
            2026, 1, "Categoria de outra conta", 50m, incomeCategoryB, userA)
        {
            Id = $"income-cross-category-{suffix}"
        };
        var groupedExpense = new Despesa(
            2026, 1, "Compra agrupadora", 30m, expenseCategoryA, userA)
        {
            Id = $"expense-parent-{suffix}"
        };
        var groupedChild = new Despesa(
            2026, 1, "Item agrupado", 30m, expenseCategoryA, userA)
        {
            Id = $"expense-child-{suffix}"
        };
        groupedChild.AdicionarDespesaAgrupadora(groupedExpense);
        var investmentA = new Investimento(
            2026, 1, "Investimento da conta A", 70m, investmentCategoryA, userA)
        {
            Id = $"investment-a-{suffix}"
        };
        var investmentB = new Investimento(
            2026, 1, "Investimento da conta B", 80m, investmentCategoryB, userB)
        {
            Id = $"investment-b-{suffix}"
        };
        var fixedCost = new CustoFixo(
            "Custo com categoria de outra conta",
            10,
            ownerA,
            incomeCategoryB.Id)
        {
            Id = $"fixed-cross-category-{suffix}"
        };

        var categoryCollection = client
            .GetDatabase("FinanMap")
            .GetCollection<Categoria>("Categoria");
        var incomeCollection = client
            .GetDatabase("FinanMap")
            .GetCollection<Rendimento>("Rendimento");
        var expenseCollection = client
            .GetDatabase("FinanMap")
            .GetCollection<Despesa>("Despesa");
        var investmentCollection = client
            .GetDatabase("FinanMap")
            .GetCollection<Investimento>("Investimento");
        var fixedCostCollection = client
            .GetDatabase("FinanMap")
            .GetCollection<CustoFixo>("CustosFixos");

        try
        {
            await categoryCollection.InsertManyAsync(
                [
                    incomeCategoryA,
                    incomeCategoryB,
                    expenseCategoryA,
                    investmentCategoryA,
                    investmentCategoryB
                ]);
            await incomeCollection.InsertManyAsync(
                [incomeA, incomeB, crossCategoryIncome]);
            await expenseCollection.InsertManyAsync(
                [groupedExpense, groupedChild]);
            await investmentCollection.InsertManyAsync(
                [investmentA, investmentB]);
            await fixedCostCollection.InsertOneAsync(fixedCost);

            var accountAIncome = await source.GetTransactionsAsync(
                ownerA,
                McpFinancialKind.Income,
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 1, 31));
            var accountBIncome = await source.GetTransactionsAsync(
                ownerB,
                McpFinancialKind.Income,
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 1, 31));
            var accountAExpenses = await source.GetTransactionsAsync(
                ownerA,
                McpFinancialKind.Expense,
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 1, 31));
            var accountAInvestments = await source.GetTransactionsAsync(
                ownerA,
                McpFinancialKind.Investment,
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 1, 31));
            var accountBInvestments = await source.GetTransactionsAsync(
                ownerB,
                McpFinancialKind.Investment,
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 1, 31));
            var accountAFixedCosts = await source.GetFixedCostsAsync(ownerA);

            Assert.Equal(
                [incomeA.Id, crossCategoryIncome.Id],
                accountAIncome.Select(item => item.Id).Order());
            Assert.Equal(incomeB.Id, Assert.Single(accountBIncome).Id);
            var minimized = Assert.Single(
                accountAIncome,
                item => item.Id == crossCategoryIncome.Id);
            Assert.Empty(minimized.CategoryId);
            Assert.Empty(minimized.CategoryName);
            Assert.Equal(groupedExpense.Id, Assert.Single(accountAExpenses).Id);
            Assert.Equal(investmentA.Id, Assert.Single(accountAInvestments).Id);
            Assert.Equal(investmentB.Id, Assert.Single(accountBInvestments).Id);
            var minimizedFixedCost = Assert.Single(accountAFixedCosts);
            Assert.Empty(minimizedFixedCost.CategoryId);
            Assert.Empty(minimizedFixedCost.CategoryName);
        }
        finally
        {
            await categoryCollection.DeleteManyAsync(item =>
                item.Id == incomeCategoryA.Id ||
                item.Id == incomeCategoryB.Id ||
                item.Id == expenseCategoryA.Id ||
                item.Id == investmentCategoryA.Id ||
                item.Id == investmentCategoryB.Id);
            await incomeCollection.DeleteManyAsync(item =>
                item.Id == incomeA.Id ||
                item.Id == incomeB.Id ||
                item.Id == crossCategoryIncome.Id);
            await expenseCollection.DeleteManyAsync(item =>
                item.Id == groupedExpense.Id ||
                item.Id == groupedChild.Id);
            await investmentCollection.DeleteManyAsync(item =>
                item.Id == investmentA.Id ||
                item.Id == investmentB.Id);
            await fixedCostCollection.DeleteOneAsync(item => item.Id == fixedCost.Id);
        }
    }
}
