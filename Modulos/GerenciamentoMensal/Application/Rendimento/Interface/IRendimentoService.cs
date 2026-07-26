using Application.DTOs;
using Application.Shared.Interfaces.Service;
using Application.Shared.Transacao.DTOs;
using Application.Mcp.Models;
using Domain.Entity;

namespace Application.Interface;

public interface IRendimentoService : IServiceBase<Rendimento, CreateRendimentoDTO, UpdateRendimentoDTO, ResultRendimentoDTO>
{
    Task<Result<ResultRendimentoDTO>> AtualizarValor(UpdateValorTransacaoDTO updateValorTransacaoDTO);
    Task<List<ResultRendimentoDTO>> ObterRendimentoMes(int mes, int ano);
    Task<McpApplicationMutationResult> AplicarMutacaoMcpAsync(
        McpWriteCommand command,
        string operationId,
        string resultHash,
        CancellationToken cancellationToken = default) =>
        Task.FromException<McpApplicationMutationResult>(
            new NotSupportedException("Mutação MCP de receita não implementada."));
}
