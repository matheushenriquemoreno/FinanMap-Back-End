using Domain.Entity;

#nullable enable annotations

namespace Domain.Repository;

public sealed record McpExpenseSnapshot(
    int Year,
    int Month,
    string Description,
    decimal Amount,
    string CategoryId,
    string? GroupingExpenseId,
    string? ExpenseOriginId,
    bool IsInstallment,
    bool IsRecurring,
    int? InstallmentNumber,
    int? InstallmentCount);

public interface IDespesaRepository : IRepositoryTransacaoBase<Despesa>
{
    Task<decimal> GetValorTotalDespesasDaAgrupadora(string idDespesaAgrupadora);
    Task<IEnumerable<Despesa>> GetPeloMes(int mes, int ano, string usuarioId, string descricao);
    Task<IEnumerable<Despesa>> GetDespesasDaAgrupadora(string idDespesaAgrupadora);
    Task<IEnumerable<Despesa>> GetDespesasDoLoteAsync(string despesaOrigemId);
    Task InsertManyAsync(IEnumerable<Despesa> despesas);
    Task UpdateManyAsync(IEnumerable<Despesa> despesas);
    Task DeleteManyAsync(IEnumerable<Despesa> despesas);
    Task<Despesa?> TryUpdateMcpAsync(
        string id,
        string userId,
        McpExpenseSnapshot expected,
        McpExpenseSnapshot proposed,
        string operationId,
        string resultHash,
        CancellationToken cancellationToken = default) =>
        Task.FromException<Despesa?>(
            new NotSupportedException("CAS MCP de despesa não implementado."));
    Task<bool> TryDeleteMcpAsync(
        string id,
        string userId,
        McpExpenseSnapshot expected,
        CancellationToken cancellationToken = default) =>
        Task.FromException<bool>(
            new NotSupportedException("Exclusão MCP de despesa não implementada."));
    Task<Despesa?> TrySynchronizeGroupingMcpAsync(
        string groupingExpenseId,
        string userId,
        decimal expectedParentAmount,
        decimal baseAmount,
        string operationId,
        string resultHash,
        CancellationToken cancellationToken = default) =>
        Task.FromException<Despesa?>(
            new NotSupportedException("Sincronização MCP de agrupamento não implementada."));
}

#nullable restore annotations
