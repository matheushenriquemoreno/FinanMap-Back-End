using Domain.Entity;

#nullable enable annotations

namespace Domain.Repository;

public sealed record McpInvestmentSnapshot(
    int Year,
    int Month,
    string Description,
    decimal Amount,
    string CategoryId);

public interface IInvestimentoRepository : IRepositoryTransacaoBase<Investimento>
{
    Task<Investimento?> TryUpdateMcpAsync(
        string id,
        string userId,
        McpInvestmentSnapshot expected,
        McpInvestmentSnapshot proposed,
        string operationId,
        string resultHash,
        CancellationToken cancellationToken = default) =>
        Task.FromException<Investimento?>(
            new NotSupportedException("CAS MCP de investimento não implementado."));
    Task<bool> TryDeleteMcpAsync(
        string id,
        string userId,
        McpInvestmentSnapshot expected,
        CancellationToken cancellationToken = default) =>
        Task.FromException<bool>(
            new NotSupportedException("Exclusão MCP de investimento não implementada."));
}

#nullable restore annotations
