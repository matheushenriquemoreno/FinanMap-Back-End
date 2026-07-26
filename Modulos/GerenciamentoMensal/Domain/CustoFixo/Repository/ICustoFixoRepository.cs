using Domain.Entity;

#nullable enable annotations

namespace Domain.Repository;

public sealed record McpFixedCostSnapshot(
    string Name,
    int DueDay,
    string? CategoryId,
    bool Active);

public interface ICustoFixoRepository : IRepositoryBase<CustoFixo>
{
    Task<List<CustoFixo>> GetByUsuarioId(string usuarioId);
    Task<bool> ExisteAtivoDuplicado(string usuarioId, string nome, int diaVencimento, string ignorarId = null);
    Task<List<CustoFixo>> GetCustosFixosAtivosPorDiaVencimento(int diaVencimento);
    Task<List<string>> GetUsuarioIdsPorDiaVencimento(int diaVencimento);
    Task<List<CustoFixo>> GetCustosFixosPorUsuariosEDiaVencimento(List<string> usuarioIds, int diaVencimento);
    Task<CustoFixo?> TryUpdateMcpAsync(
        string id,
        string userId,
        McpFixedCostSnapshot expected,
        McpFixedCostSnapshot proposed,
        string operationId,
        string resultHash,
        CancellationToken cancellationToken = default) =>
        Task.FromException<CustoFixo?>(
            new NotSupportedException("CAS MCP de custo fixo não implementado."));
    Task<bool> TryDeleteMcpAsync(
        string id,
        string userId,
        McpFixedCostSnapshot expected,
        CancellationToken cancellationToken = default) =>
        Task.FromException<bool>(
            new NotSupportedException("Exclusão MCP de custo fixo não implementada."));
}

#nullable restore annotations
