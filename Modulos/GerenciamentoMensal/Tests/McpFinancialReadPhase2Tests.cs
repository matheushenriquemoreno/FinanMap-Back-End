using Application.Mcp.Interfaces;
using Application.Mcp.Models;
using Application.Mcp.Services;
using Domain.Mcp.Entities;
using Domain.Mcp.Repositories;
using ModelContextProtocol.Server;
using WebApi.Mcp;
using Xunit;

namespace Tests;

public sealed class McpFinancialReadPhase2Tests
{
    [Fact]
    public async Task List_filters_paginates_and_persists_journal_before_financial_query()
    {
        var events = new List<string>();
        var source = new FinancialSourceFake(events,
        [
            Entry("income-1", McpFinancialKind.Income, 2026, 1, "Salário", "cat-a", "Trabalho", 5000m),
            Entry("income-2", McpFinancialKind.Income, 2026, 2, "Freela", "cat-a", "Trabalho", 1500m),
            Entry("income-3", McpFinancialKind.Income, 2026, 3, "Outro", "cat-b", "Extra", 100m)
        ]);
        var journals = new JournalFake(events);
        var service = CreateService(source, journals, events);
        var context = new McpCallContext("owner-a", "connection-a", "correlation-a");

        var first = await service.ListAsync(
            context,
            McpFinancialKind.Income,
            new McpFinancialQueryInput("2026-01", "2026-03", "cat-a", null, 1, null));

        Assert.Equal("success", first.Status);
        Assert.Equal("BRL", first.Currency);
        Assert.Equal(new DateOnly(2026, 1, 1), first.AppliedPeriod!.Start);
        Assert.Equal(new DateOnly(2026, 3, 31), first.AppliedPeriod.End);
        Assert.Single(first.Data!.Items);
        Assert.Equal("6500.00", first.Data.Total);
        Assert.NotNull(first.Page!.NextCursor);
        Assert.Equal(["journal", "connection", "source", "journal-complete"], events);
        Assert.Equal("finanmap_incomes_list", journals.LastAdded!.ToolName);
        Assert.Equal(true, journals.LastAdded.SanitizedParameters["categoryFilter"]);
        Assert.False(journals.LastAdded.SanitizedParameters.ContainsKey("category"));

        events.Clear();
        var second = await service.ListAsync(
            context,
            McpFinancialKind.Income,
            new McpFinancialQueryInput(
                "2026-01", "2026-03", "cat-a", null, 1, first.Page.NextCursor));

        Assert.Single(second.Data!.Items);
        Assert.NotEqual(first.Data.Items[0].Id, second.Data.Items[0].Id);
        Assert.Null(second.Page!.NextCursor);
    }

    [Fact]
    public async Task Mcp30_envelopes_expose_effectively_applied_filters_for_every_phase2_family()
    {
        var events = new List<string>();
        var source = new FinancialSourceFake(
            events,
            [
                Entry(
                    "income-1",
                    McpFinancialKind.Income,
                    2026,
                    1,
                    "selected income",
                    "cat-a",
                    "Category A",
                    100m),
                Entry(
                    "expense-1",
                    McpFinancialKind.Expense,
                    2026,
                    1,
                    "selected expense",
                    "cat-a",
                    "Category A",
                    50m),
                Entry(
                    "investment-1",
                    McpFinancialKind.Investment,
                    2026,
                    1,
                    "selected investment",
                    "cat-a",
                    "Category A",
                    25m)
            ],
            [
                new McpFixedCostSourceRecord(
                    "fixed-1", "Internet", 10, "cat-a", "Category A", true)
            ]);
        var service = CreateService(source, new JournalFake(events), events);
        var context = new McpCallContext(
            "owner-a", "connection-a", "correlation-mcp-30");

        var transactionResponses = new[]
        {
            await service.ListAsync(
                context,
                McpFinancialKind.Income,
                new McpFinancialQueryInput(
                    "2026-01", "2026-01", " cat-a ", " selected ", 50, null)),
            await service.ListAsync(
                context,
                McpFinancialKind.Expense,
                new McpFinancialQueryInput(
                    "2026-01", "2026-01", " cat-a ", " selected ", 50, null)),
            await service.ListAsync(
                context,
                McpFinancialKind.Investment,
                new McpFinancialQueryInput(
                    "2026-01", "2026-01", " cat-a ", " selected ", 50, null))
        };
        var fixedCosts = await service.ListFixedCostsAsync(
            context,
            new McpFixedCostQueryInput(true, " cat-a ", 50, null));
        var largest = await service.GetLargestMovementsAsync(
            context,
            new McpLargestMovementsInput(
                McpMovementKind.Expense, "2026-01", "2026-01", 2));
        var impact = await service.GetCategoryImpactAsync(
            context,
            new McpCategoryImpactInput(
                McpFinancialKind.Expense, "2026-01", "2026-01"));
        var comparison = await service.ComparePeriodsAsync(
            context,
            new McpPeriodsCompareInput(
                "2026-01",
                "2026-01",
                "2026-02",
                "2026-02",
                [
                    McpComparisonMetric.Percentage,
                    McpComparisonMetric.Totals,
                    McpComparisonMetric.Percentage
                ]));
        var summary = await service.GetSummaryAsync(
            context,
            new McpPeriodInput("2026-01", "2026-01"));

        Assert.All(
            transactionResponses,
            response =>
            {
                Assert.Equal(2, response.AppliedFilters.Count);
                Assert.Equal("cat-a", response.AppliedFilters["category"]);
                Assert.Equal("selected", response.AppliedFilters["description"]);
            });
        Assert.Equal(2, fixedCosts.AppliedFilters.Count);
        Assert.Equal(true, fixedCosts.AppliedFilters["active"]);
        Assert.Equal("cat-a", fixedCosts.AppliedFilters["category"]);
        Assert.Equal(2, largest.AppliedFilters.Count);
        Assert.Equal("Expense", largest.AppliedFilters["kind"]);
        Assert.Equal(2, largest.AppliedFilters["quantity"]);
        Assert.Single(impact.AppliedFilters);
        Assert.Equal("Expense", impact.AppliedFilters["kind"]);
        Assert.Equal(3, comparison.AppliedFilters.Count);
        Assert.Equal(
            "2026-01/2026-01",
            comparison.AppliedFilters["periodA"]);
        Assert.Equal(
            "2026-02/2026-02",
            comparison.AppliedFilters["periodB"]);
        Assert.Equal(
            ["Totals", "Percentage"],
            Assert.IsType<string[]>(comparison.AppliedFilters["metrics"]));
        Assert.Empty(summary.AppliedFilters);
    }

    [Fact]
    public async Task Cursor_is_bound_to_owner_tool_and_filters_and_period_is_limited_to_60_months()
    {
        var events = new List<string>();
        var source = new FinancialSourceFake(events,
        [
            Entry("income-1", McpFinancialKind.Income, 2026, 1, "A", "cat-a", "A", 10m),
            Entry("income-2", McpFinancialKind.Income, 2026, 2, "B", "cat-a", "A", 20m)
        ]);
        var service = CreateService(source, new JournalFake(events), events);
        var first = await service.ListAsync(
            new McpCallContext("owner-a", "connection-a", "correlation-a"),
            McpFinancialKind.Income,
            new McpFinancialQueryInput("2026-01", "2026-02", null, null, 1, null));

        var crossOwner = await service.ListAsync(
            new McpCallContext("owner-b", "connection-b", "correlation-b"),
            McpFinancialKind.Income,
            new McpFinancialQueryInput(
                "2026-01", "2026-02", null, null, 1, first.Page!.NextCursor));
        var changedFilter = await service.ListAsync(
            new McpCallContext("owner-a", "connection-a", "correlation-c"),
            McpFinancialKind.Expense,
            new McpFinancialQueryInput(
                "2026-01", "2026-02", null, null, 1, first.Page.NextCursor));
        var excessivePeriod = await service.ListAsync(
            new McpCallContext("owner-a", "connection-a", "correlation-d"),
            McpFinancialKind.Income,
            new McpFinancialQueryInput("2020-01", "2025-01", null, null, 50, null));

        Assert.Equal("rejected", crossOwner.Status);
        Assert.Equal("INVALID_CURSOR", Assert.Single(crossOwner.Errors).Code);
        Assert.Equal("rejected", changedFilter.Status);
        Assert.Equal("INVALID_CURSOR", Assert.Single(changedFilter.Errors).Code);
        Assert.Equal("rejected", excessivePeriod.Status);
        Assert.Equal("LIMIT_EXCEEDED", Assert.Single(excessivePeriod.Errors).Code);
        Assert.Contains("60 meses", excessivePeriod.Errors[0].Message);
    }

    [Fact]
    public async Task Summary_category_impact_largest_and_comparison_reconcile_exactly()
    {
        var events = new List<string>();
        var source = new FinancialSourceFake(events,
        [
            Entry("i-1", McpFinancialKind.Income, 2026, 1, "Salário", "income", "Trabalho", 1000m),
            Entry("i-2", McpFinancialKind.Income, 2026, 2, "Freela", "income", "Trabalho", 500m),
            Entry("e-1", McpFinancialKind.Expense, 2026, 1, "Casa", "home", "Casa", 300m),
            Entry("e-2", McpFinancialKind.Expense, 2026, 1, "Mercado", "food", "Mercado", 200m),
            Entry("e-3", McpFinancialKind.Expense, 2026, 2, "Casa", "home", "Casa", 400m),
            Entry("v-1", McpFinancialKind.Investment, 2026, 1, "Tesouro", "invest", "Renda fixa", 100m)
        ]);
        var service = CreateService(source, new JournalFake(events), events);
        var context = new McpCallContext("owner-a", "connection-a", "correlation-a");

        var summary = await service.GetSummaryAsync(
            context, new McpPeriodInput("2026-01", "2026-02"));
        var largest = await service.GetLargestMovementsAsync(
            context,
            new McpLargestMovementsInput(
                McpMovementKind.Expense, "2026-01", "2026-02", 2));
        var impact = await service.GetCategoryImpactAsync(
            context,
            new McpCategoryImpactInput(
                McpFinancialKind.Expense, "2026-01", "2026-02"));
        var comparison = await service.ComparePeriodsAsync(
            context,
            new McpPeriodsCompareInput(
                "2026-01",
                "2026-01",
                "2026-02",
                "2026-02",
                [
                    McpComparisonMetric.Totals,
                    McpComparisonMetric.Difference,
                    McpComparisonMetric.Percentage
                ]));

        Assert.Equal("1500.00", summary.Data!.IncomeTotal);
        Assert.Equal("900.00", summary.Data.ExpenseTotal);
        Assert.Equal("100.00", summary.Data.InvestmentTotal);
        Assert.Equal("500.00", summary.Data.Balance);
        Assert.Equal(["e-3", "e-1"], largest.Data!.Items.Select(item => item.Id));
        Assert.Equal("700.00", impact.Data!.Categories.Single(x => x.CategoryId == "home").Total);
        Assert.Equal("77.78", impact.Data.Categories.Single(x => x.CategoryId == "home").Percentage);
        Assert.Null(comparison.AppliedPeriod);
        Assert.Equal(new DateOnly(2026, 1, 1), comparison.Data!.PeriodA.Period.Start);
        Assert.Equal(new DateOnly(2026, 1, 31), comparison.Data.PeriodA.Period.End);
        Assert.Equal(new DateOnly(2026, 2, 1), comparison.Data.PeriodB.Period.Start);
        Assert.Equal(new DateOnly(2026, 2, 28), comparison.Data.PeriodB.Period.End);
        Assert.Equal("-300.00", comparison.Data.Difference!.Balance);
        Assert.Equal("-75.00", comparison.Data.Percentage!.Balance);
    }

    [Fact]
    public async Task Empty_and_fixed_cost_queries_are_explicit_and_minimized()
    {
        var events = new List<string>();
        var source = new FinancialSourceFake(
            events,
            [],
            [
                new McpFixedCostSourceRecord(
                    "fixed-a", "Internet", 10, "cat-a", "Casa", true),
                new McpFixedCostSourceRecord(
                    "fixed-b", "Academia", 5, "cat-b", "Saúde", false)
            ]);
        var service = CreateService(source, new JournalFake(events), events);
        var context = new McpCallContext("owner-a", "connection-a", "correlation-a");

        var empty = await service.ListAsync(
            context,
            McpFinancialKind.Income,
            new McpFinancialQueryInput("2026-01", "2026-01", null, null, 50, null));
        var fixedCosts = await service.ListFixedCostsAsync(
            context,
            new McpFixedCostQueryInput(true, "cat-a", 50, null));

        Assert.Equal("empty", empty.Status);
        Assert.Empty(empty.Data!.Items);
        Assert.Equal("0.00", empty.Data.Total);
        Assert.Equal("success", fixedCosts.Status);
        var fixedCost = Assert.Single(fixedCosts.Data!.Items);
        Assert.Equal("Internet", fixedCost.Name);
        Assert.Null(fixedCosts.Currency);
    }

    [Fact]
    public async Task Empty_aggregations_are_explicit_and_journal_precedes_every_source_read()
    {
        var events = new List<string>();
        var service = CreateService(
            new FinancialSourceFake(events, []),
            new JournalFake(events),
            events);
        var context = new McpCallContext(
            "owner-a", "connection-a", "correlation-empty");

        var summary = await service.GetSummaryAsync(
            context, new McpPeriodInput("2026-01", "2026-01"));

        Assert.Equal("empty", summary.Status);
        Assert.Equal("0.00", summary.Data!.Balance);
        Assert.Equal(
            ["journal", "connection", "source", "source", "source", "journal-complete"],
            events);

        events.Clear();
        var largest = await service.GetLargestMovementsAsync(
            context,
            new McpLargestMovementsInput(
                McpMovementKind.Expense, "2026-01", "2026-01", 5));
        var impact = await service.GetCategoryImpactAsync(
            context,
            new McpCategoryImpactInput(
                McpFinancialKind.Expense, "2026-01", "2026-01"));
        var comparison = await service.ComparePeriodsAsync(
            context,
            new McpPeriodsCompareInput(
                "2026-01",
                "2026-01",
                "2026-02",
                "2026-02",
                [
                    McpComparisonMetric.Totals,
                    McpComparisonMetric.Difference,
                    McpComparisonMetric.Percentage
                ]));

        Assert.Equal("empty", largest.Status);
        Assert.Equal("empty", impact.Status);
        Assert.Equal("empty", comparison.Status);
    }

    [Fact]
    public async Task Errors_distinguish_auth_scope_validation_capability_and_query_unavailability()
    {
        var context = new McpCallContext(
            "owner-a", "connection-a", "correlation-errors");
        var input = new McpFinancialQueryInput(
            "2026-01", "2026-01", null, null, 50, null);

        var auth = await CreateService(
                new FinancialSourceFake([], []),
                new JournalFake([]),
                [],
                new ThrowingConnectionValidator(new McpConnectionNotFoundException()))
            .ListAsync(context, McpFinancialKind.Income, input);
        var scope = await CreateService(
                new FinancialSourceFake([], []),
                new JournalFake([]),
                [],
                new ThrowingConnectionValidator(new McpScopeMissingException()))
            .ListAsync(context, McpFinancialKind.Income, input);
        var inactive = await CreateService(
                new FinancialSourceFake([], []),
                new JournalFake([]),
                [],
                new ThrowingConnectionValidator(new McpConnectionInactiveException()))
            .ListAsync(context, McpFinancialKind.Income, input);
        var unavailable = await CreateService(
                new ThrowingFinancialSource(),
                new JournalFake([]),
                [])
            .ListAsync(context, McpFinancialKind.Income, input);
        var limit = await CreateService(
                new FinancialSourceFake([], []),
                new JournalFake([]),
                [])
            .ListAsync(
                context,
                McpFinancialKind.Income,
                input with { Limit = 201 });
        var capability = await CreateService(
                new FinancialSourceFake([], []),
                new JournalFake([]),
                [])
            .GetLargestMovementsAsync(
                context,
                new McpLargestMovementsInput(
                    (McpMovementKind)999, "2026-01", "2026-01", 5));

        Assert.Equal("AUTH_CONNECTION_INVALID", Assert.Single(auth.Errors).Code);
        Assert.Equal("AUTH_SCOPE_MISSING", Assert.Single(scope.Errors).Code);
        Assert.Equal(
            "AUTH_CONNECTION_INACTIVE",
            Assert.Single(inactive.Errors).Code);
        Assert.Equal("QUERY_UNAVAILABLE", Assert.Single(unavailable.Errors).Code);
        Assert.True(unavailable.Errors[0].Retryable);
        Assert.DoesNotContain(
            "internal source details",
            unavailable.Errors[0].Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal("LIMIT_EXCEEDED", Assert.Single(limit.Errors).Code);
        Assert.Contains("reduza", limit.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "CAPABILITY_UNAVAILABLE",
            Assert.Single(capability.Errors).Code);
    }

    [Fact]
    public async Task Explicit_owner_is_forwarded_to_source_and_keeps_accounts_isolated()
    {
        var events = new List<string>();
        var source = new OwnerAwareFinancialSource(new Dictionary<string, McpFinancialSourceRecord>
        {
            ["owner-a"] = Entry(
                "income-a", McpFinancialKind.Income, 2026, 1,
                "Conta A", "cat-a", "A", 100m),
            ["owner-b"] = Entry(
                "income-b", McpFinancialKind.Income, 2026, 1,
                "Conta B", "cat-b", "B", 200m)
        });
        var service = CreateService(source, new JournalFake(events), events);

        var accountA = await service.ListAsync(
            new McpCallContext("owner-a", "connection-a", "correlation-a"),
            McpFinancialKind.Income,
            new McpFinancialQueryInput(
                "2026-01", "2026-01", null, null, 50, null));
        var accountB = await service.ListAsync(
            new McpCallContext("owner-b", "connection-b", "correlation-b"),
            McpFinancialKind.Income,
            new McpFinancialQueryInput(
                "2026-01", "2026-01", null, null, 50, null));

        Assert.Equal("income-a", Assert.Single(accountA.Data!.Items).Id);
        Assert.Equal("income-b", Assert.Single(accountB.Data!.Items).Id);
    }

    [Fact]
    public async Task Each_financial_domain_applies_its_supported_combined_filters()
    {
        var events = new List<string>();
        var source = new FinancialSourceFake(
            events,
            [
                Entry(
                    "income-match", McpFinancialKind.Income, 2026, 1,
                    "Freela remoto", "income-cat", "Trabalho", 100m),
                Entry(
                    "income-other", McpFinancialKind.Income, 2026, 1,
                    "Salário", "income-cat", "Trabalho", 200m),
                Entry(
                    "expense-match", McpFinancialKind.Expense, 2026, 1,
                    "Mercado mensal", "expense-cat", "Casa", 50m),
                Entry(
                    "investment-match", McpFinancialKind.Investment, 2026, 1,
                    "Tesouro direto", "investment-cat", "Renda fixa", 75m)
            ],
            [
                new McpFixedCostSourceRecord(
                    "fixed-match", "Internet", 10,
                    "fixed-cat", "Casa", true),
                new McpFixedCostSourceRecord(
                    "fixed-other", "Academia", 5,
                    "other-cat", "Saúde", false)
            ]);
        var service = CreateService(source, new JournalFake(events), events);
        var context = new McpCallContext(
            "owner-a", "connection-a", "correlation-domains");

        var income = await service.ListAsync(
            context,
            McpFinancialKind.Income,
            new McpFinancialQueryInput(
                "2026-01", "2026-01", "income-cat", "freela", 50, null));
        var expense = await service.ListAsync(
            context,
            McpFinancialKind.Expense,
            new McpFinancialQueryInput(
                "2026-01", "2026-01", "Casa", "mercado", 50, null));
        var investment = await service.ListAsync(
            context,
            McpFinancialKind.Investment,
            new McpFinancialQueryInput(
                "2026-01", "2026-01", "investment-cat", "tesouro", 50, null));
        var fixedCost = await service.ListFixedCostsAsync(
            context,
            new McpFixedCostQueryInput(true, "fixed-cat", 50, null));

        Assert.Equal("income-match", Assert.Single(income.Data!.Items).Id);
        Assert.Equal("expense-match", Assert.Single(expense.Data!.Items).Id);
        Assert.Equal(
            "investment-match",
            Assert.Single(investment.Data!.Items).Id);
        Assert.Equal("fixed-match", Assert.Single(fixedCost.Data!.Items).Id);
    }

    [Fact]
    public void All_phase_2_tools_are_closed_world_read_only_contracts()
    {
        var expected = new[]
        {
            "finanmap_incomes_list",
            "finanmap_expenses_list",
            "finanmap_investments_list",
            "finanmap_fixed_costs_list",
            "finanmap_financial_summary_get",
            "finanmap_largest_movements_get",
            "finanmap_category_impact_get",
            "finanmap_periods_compare"
        };
        var methods = typeof(McpFinancialTools)
            .GetMethods()
            .Select(method => new
            {
                Method = method,
                Attribute = method.GetCustomAttributes(typeof(McpServerToolAttribute), false)
                    .Cast<McpServerToolAttribute>()
                    .SingleOrDefault()
            })
            .Where(item => item.Attribute is not null)
            .ToArray();

        Assert.Equal(expected.Order(), methods.Select(item => item.Attribute!.Name).Order());
        Assert.All(methods, item =>
        {
            Assert.True(item.Attribute!.ReadOnly);
            Assert.True(item.Attribute.Idempotent);
            Assert.False(item.Attribute.Destructive);
            Assert.False(item.Attribute.OpenWorld);
            Assert.True(item.Attribute.UseStructuredContent);
            Assert.DoesNotContain(
                item.Method.GetParameters(),
                parameter => parameter.Name is "userId" or "usuarioId" or "proprietarioId");
        });
    }

    [Fact]
    public async Task Money_is_normalized_once_before_items_totals_aggregations_ranking_and_comparison()
    {
        var events = new List<string>();
        var source = new FinancialSourceFake(events,
        [
            Entry(
                "income-1", McpFinancialKind.Income, 2026, 1,
                "Primeira", "cat-a", "Trabalho", 1.005m),
            Entry(
                "income-2", McpFinancialKind.Income, 2026, 1,
                "Segunda", "cat-a", "Trabalho", 1.005m),
            Entry(
                "income-3", McpFinancialKind.Income, 2026, 2,
                "Terceira", "cat-a", "Trabalho", 3.005m)
        ]);
        var service = CreateService(source, new JournalFake(events), events);
        var context = new McpCallContext(
            "owner-a", "connection-a", "correlation-money");

        var list = await service.ListAsync(
            context,
            McpFinancialKind.Income,
            new McpFinancialQueryInput(
                "2026-01", "2026-01", null, null, 50, null));
        var summary = await service.GetSummaryAsync(
            context, new McpPeriodInput("2026-01", "2026-01"));
        var impact = await service.GetCategoryImpactAsync(
            context,
            new McpCategoryImpactInput(
                McpFinancialKind.Income, "2026-01", "2026-01"));
        var largest = await service.GetLargestMovementsAsync(
            context,
            new McpLargestMovementsInput(
                McpMovementKind.Income, "2026-01", "2026-01", 2));
        var comparison = await service.ComparePeriodsAsync(
            context,
            new McpPeriodsCompareInput(
                "2026-01",
                "2026-01",
                "2026-02",
                "2026-02",
                [
                    McpComparisonMetric.Totals,
                    McpComparisonMetric.Difference,
                    McpComparisonMetric.Percentage
                ]));

        Assert.All(list.Data!.Items, item => Assert.Equal("1.01", item.Amount));
        Assert.Equal("2.02", list.Data.Total);
        Assert.Equal("2.02", summary.Data!.IncomeTotal);
        Assert.Equal("2.02", impact.Data!.Total);
        Assert.All(largest.Data!.Items, item => Assert.Equal("1.01", item.Amount));
        Assert.Equal("2.02", comparison.Data!.PeriodA.Totals!.IncomeTotal);
        Assert.Equal("3.01", comparison.Data.PeriodB.Totals!.IncomeTotal);
        Assert.Equal("0.99", comparison.Data.Difference!.Income);
    }

    [Fact]
    public async Task Canonical_filter_fingerprint_prevents_delimiter_collision()
    {
        var events = new List<string>();
        var entries = new List<McpFinancialSourceRecord>
        {
            Entry(
                "income-1", McpFinancialKind.Income, 2026, 1,
                "c", "a|b", "Categoria", 10m),
            Entry(
                "income-2", McpFinancialKind.Income, 2026, 1,
                "c", "a|b", "Categoria", 20m)
        };
        var service = CreateService(
            new FinancialSourceFake(events, entries),
            new JournalFake(events),
            events);
        var context = new McpCallContext(
            "owner-a", "connection-a", "correlation-canonical");
        var first = await service.ListAsync(
            context,
            McpFinancialKind.Income,
            new McpFinancialQueryInput(
                "2026-01", "2026-01", "a|b", "c", 1, null));

        var collision = await service.ListAsync(
            context,
            McpFinancialKind.Income,
            new McpFinancialQueryInput(
                "2026-01", "2026-01", "a", "b|c", 1, first.Page!.NextCursor));

        Assert.Equal("INVALID_CURSOR", Assert.Single(collision.Errors).Code);
    }

    [Fact]
    public async Task Continuation_detects_changed_financial_and_fixed_cost_snapshots()
    {
        var events = new List<string>();
        var entries = new List<McpFinancialSourceRecord>
        {
            Entry(
                "income-1", McpFinancialKind.Income, 2026, 1,
                "A", "cat-a", "Categoria", 20m),
            Entry(
                "income-2", McpFinancialKind.Income, 2026, 1,
                "B", "cat-a", "Categoria", 10m)
        };
        var fixedCosts = new List<McpFixedCostSourceRecord>
        {
            new("fixed-1", "A", 1, "cat-a", "Categoria", true),
            new("fixed-2", "B", 2, "cat-a", "Categoria", true)
        };
        var service = CreateService(
            new FinancialSourceFake(events, entries, fixedCosts),
            new JournalFake(events),
            events);
        var context = new McpCallContext(
            "owner-a", "connection-a", "correlation-conflict");

        var financialFirst = await service.ListAsync(
            context,
            McpFinancialKind.Income,
            new McpFinancialQueryInput(
                "2026-01", "2026-01", null, null, 1, null));
        entries.Add(Entry(
            "income-new", McpFinancialKind.Income, 2026, 1,
            "Nova", "cat-a", "Categoria", 30m));
        var financialContinuation = await service.ListAsync(
            context,
            McpFinancialKind.Income,
            new McpFinancialQueryInput(
                "2026-01",
                "2026-01",
                null,
                null,
                1,
                financialFirst.Page!.NextCursor));

        var fixedFirst = await service.ListFixedCostsAsync(
            context,
            new McpFixedCostQueryInput(true, null, 1, null));
        fixedCosts.Add(new McpFixedCostSourceRecord(
            "fixed-new", "AA", 1, "cat-a", "Categoria", true));
        var fixedContinuation = await service.ListFixedCostsAsync(
            context,
            new McpFixedCostQueryInput(
                true, null, 1, fixedFirst.Page!.NextCursor));

        Assert.Equal(
            "CONFLICT_CHANGED",
            Assert.Single(financialContinuation.Errors).Code);
        Assert.Equal(
            "CONFLICT_CHANGED",
            Assert.Single(fixedContinuation.Errors).Code);
    }

    [Fact]
    public async Task Compare_metrics_are_explicit_and_invalid_capability_is_rejected()
    {
        var events = new List<string>();
        var service = CreateService(
            new FinancialSourceFake(events, []),
            new JournalFake(events),
            events);
        var context = new McpCallContext(
            "owner-a", "connection-a", "correlation-metrics");

        var differenceOnly = await service.ComparePeriodsAsync(
            context,
            new McpPeriodsCompareInput(
                "2026-01",
                "2026-01",
                "2026-02",
                "2026-02",
                [McpComparisonMetric.Difference]));
        var unsupported = await service.ComparePeriodsAsync(
            context,
            new McpPeriodsCompareInput(
                "2026-01",
                "2026-01",
                "2026-02",
                "2026-02",
                [(McpComparisonMetric)999]));

        Assert.Null(differenceOnly.AppliedPeriod);
        Assert.Null(differenceOnly.Data!.PeriodA.Totals);
        Assert.NotNull(differenceOnly.Data.Difference);
        Assert.Null(differenceOnly.Data.Percentage);
        Assert.Equal(
            "CAPABILITY_UNAVAILABLE",
            Assert.Single(unsupported.Errors).Code);
    }

    private static McpFinancialReadService CreateService(
        IMcpFinancialReadSource source,
        IMcpOperationJournalRepository journals,
        List<string> events,
        IMcpConnectionValidator? connectionValidator = null) =>
        new(
            source,
            connectionValidator ?? new ConnectionValidatorFake(events),
            journals,
            new McpAuditSanitizer(),
            new McpCursorCodec(
                "phase-2-test-cursor-signing-key-32-bytes"u8.ToArray()));

    private static McpFinancialSourceRecord Entry(
        string id,
        McpFinancialKind kind,
        int year,
        int month,
        string description,
        string categoryId,
        string categoryName,
        decimal amount) =>
        new(id, kind, year, month, description, categoryId, categoryName, amount);

    private sealed class FinancialSourceFake(
        List<string> events,
        IReadOnlyList<McpFinancialSourceRecord> entries,
        IReadOnlyList<McpFixedCostSourceRecord>? fixedCosts = null)
        : IMcpFinancialReadSource
    {
        public Task<IReadOnlyList<McpFinancialSourceRecord>> GetTransactionsAsync(
            string userId,
            McpFinancialKind kind,
            DateOnly from,
            DateOnly to,
            CancellationToken cancellationToken = default)
        {
            events.Add("source");
            return Task.FromResult<IReadOnlyList<McpFinancialSourceRecord>>(
                entries.Where(entry =>
                        entry.Kind == kind &&
                        new DateOnly(entry.Year, entry.Month, 1) >=
                        new DateOnly(from.Year, from.Month, 1) &&
                        new DateOnly(entry.Year, entry.Month, 1) <=
                        new DateOnly(to.Year, to.Month, 1))
                    .ToArray());
        }

        public Task<IReadOnlyList<McpFixedCostSourceRecord>> GetFixedCostsAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            events.Add("source");
            return Task.FromResult(fixedCosts ?? []);
        }
    }

    private sealed class ConnectionValidatorFake(List<string> events)
        : IMcpConnectionValidator
    {
        public Task<McpConnection> ValidateActiveAsync(
            string connectionId,
            string userId,
            string requiredScope,
            CancellationToken cancellationToken = default)
        {
            events.Add("connection");
            return Task.FromResult(McpConnection.CreateActive(
                userId, "authorization-a", "client-a", "Cliente", [requiredScope]));
        }
    }

    private sealed class ThrowingConnectionValidator(Exception exception)
        : IMcpConnectionValidator
    {
        public Task<McpConnection> ValidateActiveAsync(
            string connectionId,
            string userId,
            string requiredScope,
            CancellationToken cancellationToken = default) =>
            Task.FromException<McpConnection>(exception);
    }

    private sealed class ThrowingFinancialSource : IMcpFinancialReadSource
    {
        public Task<IReadOnlyList<McpFinancialSourceRecord>> GetTransactionsAsync(
            string userId,
            McpFinancialKind kind,
            DateOnly from,
            DateOnly to,
            CancellationToken cancellationToken = default) =>
            Task.FromException<IReadOnlyList<McpFinancialSourceRecord>>(
                new InvalidOperationException("internal source details"));

        public Task<IReadOnlyList<McpFixedCostSourceRecord>> GetFixedCostsAsync(
            string userId,
            CancellationToken cancellationToken = default) =>
            Task.FromException<IReadOnlyList<McpFixedCostSourceRecord>>(
                new InvalidOperationException("internal source details"));
    }

    private sealed class OwnerAwareFinancialSource(
        IReadOnlyDictionary<string, McpFinancialSourceRecord> records)
        : IMcpFinancialReadSource
    {
        public Task<IReadOnlyList<McpFinancialSourceRecord>> GetTransactionsAsync(
            string userId,
            McpFinancialKind kind,
            DateOnly from,
            DateOnly to,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<McpFinancialSourceRecord>>(
                records.TryGetValue(userId, out var record) &&
                record.Kind == kind
                    ? [record]
                    : []);

        public Task<IReadOnlyList<McpFixedCostSourceRecord>> GetFixedCostsAsync(
            string userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<McpFixedCostSourceRecord>>([]);
    }

    private sealed class JournalFake(List<string> events)
        : IMcpOperationJournalRepository
    {
        public McpOperationJournal? LastAdded { get; private set; }

        public Task AddAsync(
            McpOperationJournal journal,
            CancellationToken cancellationToken = default)
        {
            events.Add("journal");
            LastAdded = journal;
            return Task.CompletedTask;
        }

        public Task CompleteAsync(
            McpOperationJournal journal,
            object? resultSummary,
            CancellationToken cancellationToken = default)
        {
            events.Add("journal-complete");
            return Task.CompletedTask;
        }

        public Task FailAsync(
            McpOperationJournal journal,
            string errorCode,
            CancellationToken cancellationToken = default)
        {
            events.Add("journal-failed");
            return Task.CompletedTask;
        }
    }
}
