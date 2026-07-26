#nullable enable

using Application.Mcp.Models;

namespace Application.Mcp.Interfaces;

public interface IMcpFinancialReadSource
{
    Task<IReadOnlyList<McpFinancialSourceRecord>> GetTransactionsAsync(
        string userId,
        McpFinancialKind kind,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<McpFixedCostSourceRecord>> GetFixedCostsAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
