using Application.DTOs;
using Application.Shared.Interfaces.Service;
using Application.Shared.Transacao.DTOs;
using Application.Mcp.Models;
using Domain.Entity;
using Domain.Enums;

namespace Application.Interfaces;

public interface IDespesaService : IServiceBase<Despesa, CreateDespesaDTO, UpdateDespesaDTO, ResultDespesaDTO>
{
    Task<Result<ResultDespesaDTO>> AtualizarValor(UpdateValorTransacaoDTO updateValorTransacaoDTO);
    Task<List<ResultDespesaDTO>> ObterMesAno(int mes, int ano, string descricao = null);
    Task<List<ResultDespesaDTO>> ObterDespesasDaAgrupadora(string idDespesa);
    Task<Result> LancarDespesaEmLoteAsync(LancarDespesaLoteDTO dto);
    Task<Result> AtualizarDespesaEmLoteAsync(string id, AtualizarLoteDespesaDTO dto);
    Task<Result> ExcluirDespesaEmLoteAsync(string id, ModificadorLote modificador);
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
                "Mutação MCP de despesa não implementada."));
    Task<McpApplicationMutationResult> SincronizarAgrupamentoMcpAsync(
        string groupingExpenseId,
        decimal expectedParentAmount,
        decimal baseAmount,
        string operationId,
        string resultHash,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            new McpApplicationMutationResult(
                McpApplicationMutationState.Rejected,
                null,
                null,
                null,
                "MCP_GROUP_SYNC_NOT_SUPPORTED",
                "Sincronização MCP de agrupamento não implementada."));
}
