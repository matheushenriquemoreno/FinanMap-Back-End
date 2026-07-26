using Application.DTOs;
using Application.Shared.Interfaces.Service;
using Application.Shared.Transacao.DTOs;
using Application.Mcp.Models;
using Domain.Entity;

namespace Application.Interface;

public interface IInvestimentoService : IServiceBase<Investimento, CreateInvestimentoDTO, UpdateInvestimentoDTO, ResultInvestimentoDTO>
{
    Task<Result<ResultInvestimentoDTO>> AtualizarValor(UpdateValorTransacaoDTO updateValorTransacaoDTO);
    Task<List<ResultInvestimentoDTO>> ObterMesAno(int mes, int ano);
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
                "Mutação MCP de investimento não implementada."));
}
