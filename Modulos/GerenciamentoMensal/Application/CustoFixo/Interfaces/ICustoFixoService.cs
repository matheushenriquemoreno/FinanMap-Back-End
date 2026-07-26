using Application.CustoFixo.DTOs;
using Application.Shared.Interfaces.Service;
using Application.Mcp.Models;

namespace Application.CustoFixo.Interfaces;

public interface ICustoFixoService :
    IServiceBase<Domain.Entity.CustoFixo, CreateCustoFixoDTO, UpdateCustoFixoDTO, CustoFixoResponseDTO>
{
    Task<Result<List<CustoFixoResponseDTO>>> Listar();
    Task<McpApplicationMutationResult> AplicarMutacaoMcpAsync(
        McpWriteCommand command,
        string operationId,
        string resultHash,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            new McpApplicationMutationResult(
                McpApplicationMutationState.Rejected,
                null,
                null,
                null,
                "MCP_MUTATION_NOT_SUPPORTED",
                "Mutação MCP de custo fixo não implementada."));
}
