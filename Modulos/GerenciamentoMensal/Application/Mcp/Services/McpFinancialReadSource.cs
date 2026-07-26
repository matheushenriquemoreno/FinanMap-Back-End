#nullable enable

using Application.Mcp.Interfaces;
using Application.Mcp.Models;
using Domain.Entity;
using Domain.Repository;

namespace Application.Mcp.Services;

public sealed class McpFinancialReadSource(
    IRendimentoRepository incomes,
    IDespesaRepository expenses,
    IInvestimentoRepository investments,
    ICustoFixoRepository fixedCosts,
    ICategoriaRepository categories) : IMcpFinancialReadSource
{
    public async Task<IReadOnlyList<McpFinancialSourceRecord>> GetTransactionsAsync(
        string userId,
        McpFinancialKind kind,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var result = new List<McpFinancialSourceRecord>();
        foreach (var month in EnumerateMonths(from, to))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var records = kind switch
            {
                McpFinancialKind.Income =>
                    (await incomes.ObterPeloMes(month.Month, month.Year, userId))
                    .Select(item => Map(item, kind, userId)),
                McpFinancialKind.Expense =>
                    (await expenses.ObterPeloMes(month.Month, month.Year, userId))
                    .Where(item => string.IsNullOrWhiteSpace(item.IdDespesaAgrupadora))
                    .Select(item => Map(item, kind, userId)),
                McpFinancialKind.Investment =>
                    (await investments.ObterPeloMes(month.Month, month.Year, userId))
                    .Select(item => Map(item, kind, userId)),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            result.AddRange(records);
        }

        return result;
    }

    private static McpFinancialSourceRecord Map(
        Transacao item,
        McpFinancialKind kind,
        string userId)
    {
        var ownsCategory = item.Categoria?.UsuarioId == userId;
        return new McpFinancialSourceRecord(
            item.Id,
            kind,
            item.Ano,
            item.Mes,
            item.Descricao,
            ownsCategory ? item.CategoriaId : string.Empty,
            ownsCategory ? item.Categoria?.Nome ?? string.Empty : string.Empty,
            item.Valor);
    }

    public async Task<IReadOnlyList<McpFixedCostSourceRecord>> GetFixedCostsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var records = await fixedCosts.GetByUsuarioId(userId);
        var result = new List<McpFixedCostSourceRecord>(records.Count);
        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var category = string.IsNullOrWhiteSpace(record.CategoriaId)
                ? null
                : await categories.GetById(record.CategoriaId);
            var categoryName = category?.UsuarioId == userId
                ? category.Nome
                : string.Empty;
            var categoryId = category?.UsuarioId == userId
                ? record.CategoriaId ?? string.Empty
                : string.Empty;
            result.Add(new McpFixedCostSourceRecord(
                record.Id,
                record.Nome,
                record.DiaVencimento,
                categoryId,
                categoryName,
                record.Ativo));
        }

        return result;
    }

    private static IEnumerable<DateOnly> EnumerateMonths(DateOnly from, DateOnly to)
    {
        var current = new DateOnly(from.Year, from.Month, 1);
        var last = new DateOnly(to.Year, to.Month, 1);
        while (current <= last)
        {
            yield return current;
            current = current.AddMonths(1);
        }
    }
}
