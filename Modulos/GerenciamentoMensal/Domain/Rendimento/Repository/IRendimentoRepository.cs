using Domain.Entity;

namespace Domain.Repository;

public interface IRendimentoRepository : IRepositoryTransacaoBase<Rendimento>
{
    Task<Rendimento?> TryUpdateMcpAsync(
        string id,
        string userId,
        int expectedYear,
        int expectedMonth,
        string expectedDescription,
        decimal expectedAmount,
        string expectedCategoryId,
        string proposedDescription,
        decimal proposedAmount,
        string proposedCategoryId,
        string operationId,
        string resultHash,
        CancellationToken cancellationToken = default) =>
        Task.FromException<Rendimento?>(
            new NotSupportedException("CAS MCP de receita não implementado."));
    Task<bool> TryDeleteMcpAsync(
        string id,
        string userId,
        int expectedYear,
        int expectedMonth,
        string expectedDescription,
        decimal expectedAmount,
        string expectedCategoryId,
        CancellationToken cancellationToken = default) =>
        Task.FromException<bool>(
            new NotSupportedException("Exclusão MCP de receita não implementada."));
}
