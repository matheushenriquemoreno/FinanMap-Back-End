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
        cancellationToken.ThrowIfCancellationRequested();
        var records = kind switch
        {
            McpFinancialKind.Income =>
                (await incomes.ObterPorPeriodo(from, to, userId, cancellationToken))
                .Select(item => Map(item, kind, userId)),
            McpFinancialKind.Expense =>
                (await expenses.ObterPorPeriodo(from, to, userId, cancellationToken))
                .Where(item => string.IsNullOrWhiteSpace(item.IdDespesaAgrupadora))
                .Select(item => Map(item, kind, userId)),
            McpFinancialKind.Investment =>
                (await investments.ObterPorPeriodo(from, to, userId, cancellationToken))
                .Select(item => Map(item, kind, userId)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return records.ToArray();
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
        var categoryIds = records
            .Select(record => record.CategoriaId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Cast<string>()
            .ToList();
        var categoryById = (await categories.GetByIds(categoryIds))
            .Where(category => category.UsuarioId == userId)
            .ToDictionary(category => category.Id, StringComparer.Ordinal);
        var result = new List<McpFixedCostSourceRecord>(records.Count);
        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var category = string.IsNullOrWhiteSpace(record.CategoriaId)
                ? null
                : categoryById.GetValueOrDefault(record.CategoriaId);
            var categoryName = category?.Nome ?? string.Empty;
            var categoryId = category is null
                ? string.Empty
                : record.CategoriaId ?? string.Empty;
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

}
