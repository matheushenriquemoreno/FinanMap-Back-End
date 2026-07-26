using Application.DTOs;
using Application.Shared.Interfaces.Service;
using Application.Mcp.Models;
using Domain.Entity;
using Domain.Enum;

namespace Application.Interfaces
{
    public interface ICategoriaService :
        IServiceBase<Categoria, CreateCategoriaDTO, UpdateCategoriaDTO, ResultCategoriaDTO>
    {
        Task<Result<List<ResultCategoriaDTO>>> ObterCategoria(TipoCategoria tipoCategoria, string descricao);

        Task<McpApplicationMutationResult> AplicarMutacaoMcpAsync(
            McpWriteCommand command,
            string operationId,
            string resultHash,
            CancellationToken cancellationToken = default) =>
            Task.FromException<McpApplicationMutationResult>(
                new NotSupportedException("Mutação MCP de categoria não implementada."));
    }
}
