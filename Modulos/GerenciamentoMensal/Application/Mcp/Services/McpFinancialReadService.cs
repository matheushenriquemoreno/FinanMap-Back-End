#nullable enable

using System.Globalization;
using Application.Mcp.Configuration;
using Application.Mcp.Interfaces;
using Application.Mcp.Models;
using Domain.Mcp.Entities;
using Domain.Mcp.Enums;
using Domain.Mcp.Repositories;

namespace Application.Mcp.Services;

public sealed class McpFinancialReadService(
    IMcpFinancialReadSource source,
    IMcpConnectionValidator connections,
    IMcpOperationJournalRepository journals,
    McpAuditSanitizer sanitizer,
    McpCursorCodec cursors)
{
    private const int DefaultLimit = 50;
    private const int MaximumLimit = 200;
    private const int MaximumMonths = 60;

    public Task<McpToolEnvelope<McpFinancialListData>> ListAsync(
        McpCallContext context,
        McpFinancialKind kind,
        McpFinancialQueryInput input,
        CancellationToken cancellationToken = default)
    {
        var toolName = kind switch
        {
            McpFinancialKind.Income => "finanmap_incomes_list",
            McpFinancialKind.Expense => "finanmap_expenses_list",
            McpFinancialKind.Investment => "finanmap_investments_list",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var audit = QueryAuditParameters(
            kind, input.From, input.To, input.Category, input.Description,
            input.Limit, input.Cursor);
        return ExecuteAsync(
            context,
            toolName,
            audit,
            async () =>
            {
                var periodResult = ParsePeriod(input.From, input.To);
                if (periodResult.Error is { } periodError)
                    return Rejected<McpFinancialListData>(
                        context, periodError, periodResult.Period);
                var period = periodResult.Period!;
                var limitResult = NormalizeLimit(input.Limit);
                if (limitResult.Error is { } limitError)
                    return Rejected<McpFinancialListData>(context, limitError, period);
                var limit = limitResult.Limit;
                var fingerprint = McpCursorCodec.CanonicalFingerprint(new
                {
                    from = input.From,
                    to = input.To,
                    category = input.Category?.Trim(),
                    description = input.Description?.Trim(),
                    limit,
                    kind = kind.ToString()
                });
                if (!cursors.TryDecode(
                        input.Cursor,
                        context.UserId,
                        toolName,
                        fingerprint,
                        out var offset,
                        out var cursorSnapshot))
                {
                    return Rejected<McpFinancialListData>(
                        context,
                        Error(
                            "INVALID_CURSOR",
                            "O cursor não é válido para esta conta, ferramenta e filtros.",
                            "cursor"),
                        period);
                }

                var records = await source.GetTransactionsAsync(
                    context.UserId,
                    kind,
                    period.Start!.Value,
                    period.End!.Value,
                    cancellationToken);
                var filtered = Filter(
                        records.Select(Normalize),
                        input.Category,
                        input.Description)
                    .OrderByDescending(item => item.Year)
                    .ThenByDescending(item => item.Month)
                    .ThenByDescending(item => item.Amount)
                    .ThenBy(item => item.Id, StringComparer.Ordinal)
                    .ToArray();
                var snapshot = TransactionSnapshot(filtered);
                if (SnapshotChanged(cursorSnapshot, snapshot))
                {
                    return Rejected<McpFinancialListData>(
                        context,
                        Error(
                            "CONFLICT_CHANGED",
                            "Os movimentos mudaram desde a página anterior; reinicie a consulta.",
                            "cursor"),
                        period);
                }
                if (offset > filtered.Length)
                {
                    return Rejected<McpFinancialListData>(
                        context,
                        Error(
                            "INVALID_CURSOR",
                            "O cursor não é mais válido; reinicie a consulta.",
                            "cursor"),
                        period);
                }

                var page = filtered.Skip(offset).Take(limit).ToArray();
                var hasMore = offset + page.Length < filtered.Length;
                var nextCursor = hasMore
                    ? cursors.Encode(
                        offset + page.Length,
                        context.UserId,
                        toolName,
                        fingerprint,
                        snapshot)
                    : null;
                var items = page.Select(Map).ToArray();
                return Envelope(
                    context,
                    items.Length == 0 ? "empty" : "success",
                    new McpFinancialListData(
                        items,
                        items.Length,
                        Money(filtered.Sum(item => item.Amount))),
                    period,
                    "BRL",
                    AppliedFilters(input.Category, input.Description),
                    new McpPage(limit, items.Length, nextCursor, hasMore));
            },
            cancellationToken);
    }

    public Task<McpToolEnvelope<McpFixedCostListData>> ListFixedCostsAsync(
        McpCallContext context,
        McpFixedCostQueryInput input,
        CancellationToken cancellationToken = default)
    {
        const string toolName = "finanmap_fixed_costs_list";
        var audit = sanitizer.Sanitize(new Dictionary<string, object?>
        {
            ["active"] = input.Active,
            ["categoryFilter"] = !string.IsNullOrWhiteSpace(input.Category),
            ["limit"] = input.Limit,
            ["cursorPresent"] = !string.IsNullOrWhiteSpace(input.Cursor)
        });
        return ExecuteAsync(
            context,
            toolName,
            audit,
            async () =>
            {
                var limitResult = NormalizeLimit(input.Limit);
                if (limitResult.Error is { } limitError)
                    return Rejected<McpFixedCostListData>(context, limitError, null);
                var limit = limitResult.Limit;
                var fingerprint = McpCursorCodec.CanonicalFingerprint(new
                {
                    active = input.Active,
                    category = input.Category?.Trim(),
                    limit
                });
                if (!cursors.TryDecode(
                        input.Cursor,
                        context.UserId,
                        toolName,
                        fingerprint,
                        out var offset,
                        out var cursorSnapshot))
                {
                    return Rejected<McpFixedCostListData>(
                        context,
                        Error(
                            "INVALID_CURSOR",
                            "O cursor não é válido para esta conta, ferramenta e filtros.",
                            "cursor"),
                        null);
                }

                var sourceRecords = await source.GetFixedCostsAsync(
                    context.UserId, cancellationToken);
                var filtered = sourceRecords
                    .Where(item => input.Active is null || item.Active == input.Active)
                    .Where(item => MatchesCategory(
                        item.CategoryId, item.CategoryName, input.Category))
                    .OrderBy(item => item.DueDay)
                    .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.Id, StringComparer.Ordinal)
                    .ToArray();
                var snapshot = FixedCostSnapshot(filtered);
                if (SnapshotChanged(cursorSnapshot, snapshot))
                {
                    return Rejected<McpFixedCostListData>(
                        context,
                        Error(
                            "CONFLICT_CHANGED",
                            "Os custos fixos mudaram desde a página anterior; reinicie a consulta.",
                            "cursor"),
                        null);
                }
                if (offset > filtered.Length)
                {
                    return Rejected<McpFixedCostListData>(
                        context,
                        Error(
                            "INVALID_CURSOR",
                            "O cursor não é mais válido; reinicie a consulta.",
                            "cursor"),
                        null);
                }

                var page = filtered.Skip(offset).Take(limit).ToArray();
                var hasMore = offset + page.Length < filtered.Length;
                var nextCursor = hasMore
                    ? cursors.Encode(
                        offset + page.Length,
                        context.UserId,
                        toolName,
                        fingerprint,
                        snapshot)
                    : null;
                var items = page.Select(item => new McpFixedCostItem(
                    item.Id,
                    item.Name,
                    item.DueDay,
                    item.CategoryId,
                    item.CategoryName,
                    item.Active)).ToArray();
                return Envelope(
                    context,
                    items.Length == 0 ? "empty" : "success",
                    new McpFixedCostListData(items, items.Length),
                    null,
                    null,
                    new Dictionary<string, object?>
                    {
                        ["active"] = input.Active,
                        ["category"] = NormalizeFilter(input.Category)
                    },
                    new McpPage(limit, items.Length, nextCursor, hasMore));
            },
            cancellationToken);
    }

    public Task<McpToolEnvelope<McpFinancialSummaryData>> GetSummaryAsync(
        McpCallContext context,
        McpPeriodInput input,
        CancellationToken cancellationToken = default) =>
        ExecutePeriodAggregationAsync(
            context,
            "finanmap_financial_summary_get",
            input,
            async period =>
            {
                var summary = await LoadSummaryAsync(context.UserId, period, cancellationToken);
                return Envelope(
                    context,
                    summary.Count == 0 ? "empty" : "success",
                    summary.Data,
                    period,
                    "BRL",
                    new Dictionary<string, object?>(),
                    null);
            },
            cancellationToken);

    public Task<McpToolEnvelope<McpLargestMovementsData>> GetLargestMovementsAsync(
        McpCallContext context,
        McpLargestMovementsInput input,
        CancellationToken cancellationToken = default)
    {
        const string toolName = "finanmap_largest_movements_get";
        var audit = sanitizer.Sanitize(new Dictionary<string, object?>
        {
            ["kind"] = input.Kind.ToString(),
            ["from"] = input.From,
            ["to"] = input.To,
            ["quantity"] = input.Quantity
        });
        return ExecuteAsync(
            context,
            toolName,
            audit,
            async () =>
            {
                var periodResult = ParsePeriod(input.From, input.To);
                if (periodResult.Error is { } periodError)
                    return Rejected<McpLargestMovementsData>(
                        context, periodError, periodResult.Period);
                var period = periodResult.Period!;
                if (!Enum.IsDefined(input.Kind))
                {
                    return Rejected<McpLargestMovementsData>(
                        context,
                        Error(
                            "CAPABILITY_UNAVAILABLE",
                            "Maiores movimentos estão disponíveis somente para receitas e despesas.",
                            "kind"),
                        period);
                }
                var financialKind = input.Kind switch
                {
                    McpMovementKind.Income => McpFinancialKind.Income,
                    McpMovementKind.Expense => McpFinancialKind.Expense,
                    _ => throw new ArgumentOutOfRangeException(nameof(input.Kind))
                };
                if (input.Quantity is < 1 or > 50)
                {
                    return Rejected<McpLargestMovementsData>(
                        context,
                        Error(
                            "LIMIT_EXCEEDED",
                            "A quantidade deve estar entre 1 e 50; reduza o valor solicitado.",
                            "quantity"),
                        period);
                }

                var records = await source.GetTransactionsAsync(
                    context.UserId,
                    financialKind,
                    period.Start!.Value,
                    period.End!.Value,
                    cancellationToken);
                var items = records
                    .Select(Normalize)
                    .OrderByDescending(item => item.Amount)
                    .ThenByDescending(item => item.Year)
                    .ThenByDescending(item => item.Month)
                    .ThenBy(item => item.Id, StringComparer.Ordinal)
                    .Take(input.Quantity)
                    .Select(Map)
                    .ToArray();
                return Envelope(
                    context,
                    items.Length == 0 ? "empty" : "success",
                    new McpLargestMovementsData(input.Kind, items, items.Length),
                    period,
                    "BRL",
                    new Dictionary<string, object?>
                    {
                        ["kind"] = input.Kind.ToString(),
                        ["quantity"] = input.Quantity
                    },
                    null);
            },
            cancellationToken);
    }

    public Task<McpToolEnvelope<McpCategoryImpactData>> GetCategoryImpactAsync(
        McpCallContext context,
        McpCategoryImpactInput input,
        CancellationToken cancellationToken = default)
    {
        const string toolName = "finanmap_category_impact_get";
        var audit = sanitizer.Sanitize(new Dictionary<string, object?>
        {
            ["kind"] = input.Kind.ToString(),
            ["from"] = input.From,
            ["to"] = input.To
        });
        return ExecuteAsync(
            context,
            toolName,
            audit,
            async () =>
            {
                var periodResult = ParsePeriod(input.From, input.To);
                if (periodResult.Error is { } periodError)
                    return Rejected<McpCategoryImpactData>(
                        context, periodError, periodResult.Period);
                var period = periodResult.Period!;
                var records = await source.GetTransactionsAsync(
                    context.UserId,
                    input.Kind,
                    period.Start!.Value,
                    period.End!.Value,
                    cancellationToken);
                var normalizedRecords = records.Select(Normalize).ToArray();
                var total = normalizedRecords.Sum(item => item.Amount);
                var categories = normalizedRecords
                    .GroupBy(item => new { item.CategoryId, item.CategoryName })
                    .Select(group =>
                    {
                        var categoryTotal = group.Sum(item => item.Amount);
                        return new McpCategoryImpactItem(
                            group.Key.CategoryId,
                            group.Key.CategoryName,
                            Money(categoryTotal),
                            Percentage(categoryTotal, total));
                    })
                    .OrderByDescending(item => decimal.Parse(
                        item.Total, CultureInfo.InvariantCulture))
                    .ThenBy(item => item.CategoryName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.CategoryId, StringComparer.Ordinal)
                    .ToArray();
                return Envelope(
                    context,
                    categories.Length == 0 ? "empty" : "success",
                    new McpCategoryImpactData(input.Kind, categories, Money(total)),
                    period,
                    "BRL",
                    new Dictionary<string, object?> { ["kind"] = input.Kind.ToString() },
                    null);
            },
            cancellationToken);
    }

    public Task<McpToolEnvelope<McpPeriodsCompareData>> ComparePeriodsAsync(
        McpCallContext context,
        McpPeriodsCompareInput input,
        CancellationToken cancellationToken = default)
    {
        const string toolName = "finanmap_periods_compare";
        var audit = sanitizer.Sanitize(new Dictionary<string, object?>
        {
            ["fromA"] = input.FromA,
            ["toA"] = input.ToA,
            ["fromB"] = input.FromB,
            ["toB"] = input.ToB,
            ["metrics"] = input.Metrics is null
                ? new[] { "all" }
                : input.Metrics.Select(metric => metric.ToString()).ToArray()
        });
        return ExecuteAsync(
            context,
            toolName,
            audit,
            async () =>
            {
                var first = ParsePeriod(input.FromA, input.ToA);
                if (first.Error is { } firstError)
                    return Rejected<McpPeriodsCompareData>(
                        context, firstError with { Field = "periodA" }, first.Period);
                var second = ParsePeriod(input.FromB, input.ToB);
                if (second.Error is { } secondError)
                    return Rejected<McpPeriodsCompareData>(
                        context, secondError with { Field = "periodB" }, second.Period);
                var periodA = first.Period!;
                var periodB = second.Period!;
                var metricResult = NormalizeMetrics(input.Metrics);
                if (metricResult.Error is { } metricError)
                {
                    return Rejected<McpPeriodsCompareData>(
                        context, metricError, null);
                }
                var metrics = metricResult.Metrics;
                var summaryA = await LoadSummaryAsync(
                    context.UserId, periodA, cancellationToken);
                var summaryB = await LoadSummaryAsync(
                    context.UserId, periodB, cancellationToken);
                var incomeA = ParseMoney(summaryA.Data.IncomeTotal);
                var expenseA = ParseMoney(summaryA.Data.ExpenseTotal);
                var investmentA = ParseMoney(summaryA.Data.InvestmentTotal);
                var balanceA = ParseMoney(summaryA.Data.Balance);
                var incomeB = ParseMoney(summaryB.Data.IncomeTotal);
                var expenseB = ParseMoney(summaryB.Data.ExpenseTotal);
                var investmentB = ParseMoney(summaryB.Data.InvestmentTotal);
                var balanceB = ParseMoney(summaryB.Data.Balance);
                var includeTotals = metrics.Contains(McpComparisonMetric.Totals);
                var includeDifference = metrics.Contains(
                    McpComparisonMetric.Difference);
                var includePercentage = metrics.Contains(
                    McpComparisonMetric.Percentage);
                var warnings = includePercentage
                    ? PercentageWarnings(
                        incomeA, expenseA, investmentA, balanceA)
                    : [];
                var data = new McpPeriodsCompareData(
                    metrics,
                    new McpComparedPeriod(
                        periodA,
                        includeTotals ? summaryA.Data : null),
                    new McpComparedPeriod(
                        periodB,
                        includeTotals ? summaryB.Data : null),
                    includeDifference
                        ? new McpFinancialDifference(
                            Money(incomeB - incomeA),
                            Money(expenseB - expenseA),
                            Money(investmentB - investmentA),
                            Money(balanceB - balanceA))
                        : null,
                    includePercentage
                        ? new McpFinancialPercentage(
                            PercentageOrNull(incomeB - incomeA, incomeA),
                            PercentageOrNull(expenseB - expenseA, expenseA),
                            PercentageOrNull(
                                investmentB - investmentA,
                                investmentA),
                            PercentageOrNull(balanceB - balanceA, balanceA))
                        : null);
                return Envelope(
                    context,
                    summaryA.Count + summaryB.Count == 0 ? "empty" : "success",
                    data,
                    null,
                    "BRL",
                    new Dictionary<string, object?>
                    {
                        ["periodA"] = $"{input.FromA}/{input.ToA}",
                        ["periodB"] = $"{input.FromB}/{input.ToB}",
                        ["metrics"] = metrics.Select(metric => metric.ToString()).ToArray()
                    },
                    null,
                    warnings);
            },
            cancellationToken);
    }

    private Task<McpToolEnvelope<T>> ExecutePeriodAggregationAsync<T>(
        McpCallContext context,
        string toolName,
        McpPeriodInput input,
        Func<McpAppliedPeriod, Task<McpToolEnvelope<T>>> action,
        CancellationToken cancellationToken)
    {
        var audit = sanitizer.Sanitize(new Dictionary<string, object?>
        {
            ["from"] = input.From,
            ["to"] = input.To
        });
        return ExecuteAsync(
            context,
            toolName,
            audit,
            async () =>
            {
                var periodResult = ParsePeriod(input.From, input.To);
                return periodResult.Error is { } error
                    ? Rejected<T>(context, error, periodResult.Period)
                    : await action(periodResult.Period!);
            },
            cancellationToken);
    }

    private async Task<McpToolEnvelope<T>> ExecuteAsync<T>(
        McpCallContext context,
        string toolName,
        IReadOnlyDictionary<string, object?> parameters,
        Func<Task<McpToolEnvelope<T>>> query,
        CancellationToken cancellationToken)
    {
        var journal = McpOperationJournal.Start(
            context.UserId,
            context.ConnectionId,
            context.CorrelationId,
            toolName,
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
            await journals.AddAsync(journal, cancellationToken);
        }
        catch (Exception exception)
        {
            throw new McpJournalUnavailableException(exception);
        }

        try
        {
            await connections.ValidateActiveAsync(
                context.ConnectionId,
                context.UserId,
                "mcp:read",
                cancellationToken);
            var response = await query();
            journal.Complete(new Dictionary<string, object?>
            {
                ["status"] = response.Status,
                ["count"] = ResponseCount(response.Data)
            });
            await journals.CompleteAsync(journal, journal.ResultSummary, cancellationToken);
            return response;
        }
        catch (OperationCanceledException)
        {
            journal.Fail("QUERY_CANCELLED");
            await journals.FailAsync(journal, "QUERY_CANCELLED", cancellationToken);
            throw;
        }
        catch (McpConnectionNotFoundException)
        {
            const string code = "AUTH_CONNECTION_INVALID";
            journal.Fail(code);
            await journals.FailAsync(journal, code, cancellationToken);
            return Rejected<T>(
                context,
                Error(
                    code,
                    "A conexão MCP não é válida para esta conta.",
                    null),
                null);
        }
        catch (McpConnectionInactiveException)
        {
            const string code = "AUTH_CONNECTION_INACTIVE";
            journal.Fail(code);
            await journals.FailAsync(journal, code, cancellationToken);
            return Rejected<T>(
                context,
                Error(
                    code,
                    "A conexão MCP está inativa ou expirada; reconecte a integração.",
                    null),
                null);
        }
        catch (McpScopeMissingException)
        {
            const string code = "AUTH_SCOPE_MISSING";
            journal.Fail(code);
            await journals.FailAsync(journal, code, cancellationToken);
            return Rejected<T>(
                context,
                Error(
                    code,
                    "A conexão MCP não possui o escopo mcp:read necessário.",
                    null),
                null);
        }
        catch
        {
            journal.Fail("QUERY_UNAVAILABLE");
            await journals.FailAsync(journal, "QUERY_UNAVAILABLE", cancellationToken);
            return Rejected<T>(
                context,
                Error(
                    "QUERY_UNAVAILABLE",
                    "A consulta financeira está temporariamente indisponível; tente novamente.",
                    null,
                    true),
                null);
        }
    }

    private async Task<SummaryResult> LoadSummaryAsync(
        string userId,
        McpAppliedPeriod period,
        CancellationToken cancellationToken)
    {
        var income = await source.GetTransactionsAsync(
            userId, McpFinancialKind.Income, period.Start!.Value, period.End!.Value,
            cancellationToken);
        var expenses = await source.GetTransactionsAsync(
            userId, McpFinancialKind.Expense, period.Start.Value, period.End.Value,
            cancellationToken);
        var investments = await source.GetTransactionsAsync(
            userId, McpFinancialKind.Investment, period.Start.Value, period.End.Value,
            cancellationToken);
        var incomeTotal = income.Sum(item => NormalizeMoney(item.Amount));
        var expenseTotal = expenses.Sum(item => NormalizeMoney(item.Amount));
        var investmentTotal = investments.Sum(item => NormalizeMoney(item.Amount));
        return new SummaryResult(
            new McpFinancialSummaryData(
                Money(incomeTotal),
                Money(expenseTotal),
                Money(investmentTotal),
                Money(incomeTotal - expenseTotal - investmentTotal)),
            income.Count + expenses.Count + investments.Count);
    }

    private static IEnumerable<McpFinancialSourceRecord> Filter(
        IEnumerable<McpFinancialSourceRecord> sourceRecords,
        string? category,
        string? description) =>
        sourceRecords
            .Where(item => MatchesCategory(
                item.CategoryId, item.CategoryName, category))
            .Where(item => Contains(item.Description, description));

    private static bool MatchesCategory(
        string categoryId,
        string categoryName,
        string? filter) =>
        string.IsNullOrWhiteSpace(filter) ||
        string.Equals(categoryId, filter.Trim(), StringComparison.Ordinal) ||
        string.Equals(categoryName, filter.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string value, string? filter) =>
        string.IsNullOrWhiteSpace(filter) ||
        value.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);

    private static McpFinancialItem Map(McpFinancialSourceRecord item) =>
        new(
            item.Id,
            $"{item.Year:D4}-{item.Month:D2}",
            item.Description,
            item.CategoryId,
            item.CategoryName,
            Money(item.Amount));

    private static McpFinancialSourceRecord Normalize(
        McpFinancialSourceRecord item) =>
        item with { Amount = NormalizeMoney(item.Amount) };

    private static string TransactionSnapshot(
        IReadOnlyList<McpFinancialSourceRecord> records) =>
        McpCursorCodec.CanonicalFingerprint(records.Select(item => new
        {
            id = item.Id,
            kind = item.Kind.ToString(),
            year = item.Year,
            month = item.Month,
            description = item.Description,
            categoryId = item.CategoryId,
            categoryName = item.CategoryName,
            amount = item.Amount
        }).ToArray());

    private static string FixedCostSnapshot(
        IReadOnlyList<McpFixedCostSourceRecord> records) =>
        McpCursorCodec.CanonicalFingerprint(records.Select(item => new
        {
            id = item.Id,
            name = item.Name,
            dueDay = item.DueDay,
            categoryId = item.CategoryId,
            categoryName = item.CategoryName,
            active = item.Active
        }).ToArray());

    private static bool SnapshotChanged(
        string? cursorSnapshot,
        string currentSnapshot) =>
        cursorSnapshot is not null &&
        !string.Equals(
            cursorSnapshot,
            currentSnapshot,
            StringComparison.Ordinal);

    private IReadOnlyDictionary<string, object?> QueryAuditParameters(
        McpFinancialKind kind,
        string from,
        string to,
        string? category,
        string? description,
        int limit,
        string? cursor) =>
        sanitizer.Sanitize(new Dictionary<string, object?>
        {
            ["kind"] = kind.ToString(),
            ["from"] = from,
            ["to"] = to,
            ["categoryFilter"] = !string.IsNullOrWhiteSpace(category),
            ["descriptionFilter"] = !string.IsNullOrWhiteSpace(description),
            ["limit"] = limit,
            ["cursorPresent"] = !string.IsNullOrWhiteSpace(cursor)
        });

    private static IReadOnlyDictionary<string, object?> AppliedFilters(
        string? category,
        string? description) =>
        new Dictionary<string, object?>
        {
            ["category"] = NormalizeFilter(category),
            ["description"] = NormalizeFilter(description)
        };

    private static string? NormalizeFilter(string? filter) =>
        string.IsNullOrWhiteSpace(filter) ? null : filter.Trim();

    private static (McpAppliedPeriod? Period, McpToolError? Error) ParsePeriod(
        string from,
        string to)
    {
        if (!TryParseMonth(from, out var start) || !TryParseMonth(to, out var endMonth))
        {
            return (
                null,
                Error(
                    "VALIDATION_REQUIRED",
                    "Informe o período no formato YYYY-MM.",
                    "period"));
        }

        var end = new DateOnly(
            endMonth.Year,
            endMonth.Month,
            DateTime.DaysInMonth(endMonth.Year, endMonth.Month));
        if (start > end)
        {
            return (
                null,
                Error(
                    "VALIDATION_REQUIRED",
                    "O início do período deve ser anterior ou igual ao fim.",
                    "period"));
        }

        var months = (end.Year - start.Year) * 12 + end.Month - start.Month + 1;
        var period = new McpAppliedPeriod(start, end);
        if (months > MaximumMonths)
        {
            return (
                period,
                Error(
                    "LIMIT_EXCEEDED",
                    "O período máximo é de 60 meses; reduza ou divida a consulta.",
                    "period"));
        }

        return (period, null);
    }

    private static bool TryParseMonth(string value, out DateOnly month)
    {
        month = default;
        if (!DateOnly.TryParseExact(
                $"{value}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return false;
        }

        month = parsed;
        return true;
    }

    private static (int Limit, McpToolError? Error) NormalizeLimit(int limit)
    {
        if (limit == 0)
            return (DefaultLimit, null);
        return limit is < 1 or > MaximumLimit
            ? (
                limit,
                Error(
                    "LIMIT_EXCEEDED",
                    "O limite deve estar entre 1 e 200; reduza o valor solicitado.",
                    "limit"))
            : (limit, null);
    }

    private static (
        IReadOnlyList<McpComparisonMetric> Metrics,
        McpToolError? Error) NormalizeMetrics(
            IReadOnlyList<McpComparisonMetric>? requested)
    {
        if (requested is null)
        {
            return (
                [
                    McpComparisonMetric.Totals,
                    McpComparisonMetric.Difference,
                    McpComparisonMetric.Percentage
                ],
                null);
        }
        if (requested.Count == 0)
        {
            return (
                [],
                Error(
                    "VALIDATION_REQUIRED",
                    "Informe ao menos uma métrica de comparação.",
                    "metrics"));
        }
        if (requested.Any(metric => !Enum.IsDefined(metric)))
        {
            return (
                [],
                Error(
                    "CAPABILITY_UNAVAILABLE",
                    "A métrica solicitada não está disponível; use Totals, Difference ou Percentage.",
                    "metrics"));
        }

        return (
            requested
                .Distinct()
                .OrderBy(metric => metric)
                .ToArray(),
            null);
    }

    private static McpToolEnvelope<T> Envelope<T>(
        McpCallContext context,
        string status,
        T data,
        McpAppliedPeriod? period,
        string? currency,
        IReadOnlyDictionary<string, object?> filters,
        McpPage? page,
        IReadOnlyList<string>? warnings = null) =>
        new(
            McpProtocolContract.SchemaVersion,
            context.CorrelationId,
            status,
            data,
            period,
            currency,
            filters,
            page,
            warnings ?? [],
            []);

    private static McpToolEnvelope<T> Rejected<T>(
        McpCallContext context,
        McpToolError error,
        McpAppliedPeriod? period) =>
        new(
            McpProtocolContract.SchemaVersion,
            context.CorrelationId,
            "rejected",
            default,
            period,
            null,
            new Dictionary<string, object?>(),
            null,
            [],
            [error]);

    private static McpToolError Error(
        string code,
        string message,
        string? field,
        bool retryable = false) =>
        new(code, message, field, retryable);

    private static decimal NormalizeMoney(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Money(decimal value) =>
        NormalizeMoney(value).ToString("0.00", CultureInfo.InvariantCulture);

    private static decimal ParseMoney(string value) =>
        decimal.Parse(value, CultureInfo.InvariantCulture);

    private static string Percentage(decimal numerator, decimal denominator) =>
        denominator == 0
            ? "0.00"
            : decimal.Round(
                numerator / denominator * 100m,
                2,
                MidpointRounding.AwayFromZero).ToString(
                "0.00",
                CultureInfo.InvariantCulture);

    private static string? PercentageOrNull(
        decimal numerator,
        decimal denominator) =>
        denominator == 0
            ? null
            : Percentage(numerator, Math.Abs(denominator));

    private static IReadOnlyList<string> PercentageWarnings(
        decimal income,
        decimal expense,
        decimal investment,
        decimal balance)
    {
        var warnings = new List<string>();
        if (income == 0)
            warnings.Add("Percentual de receitas indisponível porque o total do período A é zero.");
        if (expense == 0)
            warnings.Add("Percentual de despesas indisponível porque o total do período A é zero.");
        if (investment == 0)
            warnings.Add("Percentual de investimentos indisponível porque o total do período A é zero.");
        if (balance == 0)
            warnings.Add("Percentual de saldo indisponível porque o saldo do período A é zero.");
        return warnings;
    }

    private static int ResponseCount<T>(T? data) =>
        data switch
        {
            McpFinancialListData list => list.Count,
            McpFixedCostListData fixedCosts => fixedCosts.Count,
            McpLargestMovementsData largest => largest.Count,
            McpCategoryImpactData impact => impact.Categories.Count,
            null => 0,
            _ => 1
        };

    private sealed record SummaryResult(McpFinancialSummaryData Data, int Count);
}
