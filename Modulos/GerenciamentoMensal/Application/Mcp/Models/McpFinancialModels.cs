#nullable enable

using System.Text.Json.Serialization;

namespace Application.Mcp.Models;

public enum McpFinancialKind
{
    Income,
    Expense,
    Investment
}

public enum McpMovementKind
{
    Income,
    Expense
}

public enum McpComparisonMetric
{
    Totals,
    Difference,
    Percentage
}

public sealed record McpFinancialSourceRecord(
    string Id,
    McpFinancialKind Kind,
    int Year,
    int Month,
    string Description,
    string CategoryId,
    string CategoryName,
    decimal Amount);

public sealed record McpFixedCostSourceRecord(
    string Id,
    string Name,
    int DueDay,
    string CategoryId,
    string CategoryName,
    bool Active);

public sealed record McpPeriodInput(string From, string To);

public sealed record McpFinancialQueryInput(
    string From,
    string To,
    string? Category,
    string? Description,
    int Limit = 50,
    string? Cursor = null);

public sealed record McpFixedCostQueryInput(
    bool? Active,
    string? Category,
    int Limit = 50,
    string? Cursor = null);

public sealed record McpLargestMovementsInput(
    McpMovementKind Kind,
    string From,
    string To,
    int Quantity = 5);

public sealed record McpCategoryImpactInput(
    McpFinancialKind Kind,
    string From,
    string To);

public sealed record McpPeriodsCompareInput(
    string FromA,
    string ToA,
    string FromB,
    string ToB,
    IReadOnlyList<McpComparisonMetric>? Metrics);

public sealed record McpFinancialItem(
    string Id,
    string Period,
    string Description,
    string CategoryId,
    string CategoryName,
    string Amount);

public sealed record McpFinancialListData(
    IReadOnlyList<McpFinancialItem> Items,
    int Count,
    string Total);

public sealed record McpFixedCostItem(
    string Id,
    string Name,
    int DueDay,
    string CategoryId,
    string CategoryName,
    bool Active);

public sealed record McpFixedCostListData(
    IReadOnlyList<McpFixedCostItem> Items,
    int Count);

public sealed record McpFinancialSummaryData(
    string IncomeTotal,
    string ExpenseTotal,
    string InvestmentTotal,
    string Balance);

public sealed record McpLargestMovementsData(
    McpMovementKind Kind,
    IReadOnlyList<McpFinancialItem> Items,
    int Count);

public sealed record McpCategoryImpactItem(
    string CategoryId,
    string CategoryName,
    string Total,
    string Percentage);

public sealed record McpCategoryImpactData(
    McpFinancialKind Kind,
    IReadOnlyList<McpCategoryImpactItem> Categories,
    string Total);

public sealed record McpComparedPeriod(
    McpAppliedPeriod Period,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    McpFinancialSummaryData? Totals);

public sealed record McpFinancialDifference(
    string Income,
    string Expense,
    string Investment,
    string Balance);

public sealed record McpFinancialPercentage(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    string? Income,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    string? Expense,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    string? Investment,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    string? Balance);

public sealed record McpPeriodsCompareData(
    IReadOnlyList<McpComparisonMetric> Metrics,
    McpComparedPeriod PeriodA,
    McpComparedPeriod PeriodB,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    McpFinancialDifference? Difference,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    McpFinancialPercentage? Percentage);
