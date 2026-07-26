using Application.DTOs;
using Application.Interfaces;
using Application.Mcp.Models;
using Domain.Compartilhamento.Entity;
using Domain.Entity;
using Domain.Enum;
using Domain.Login.Interfaces;
using Domain.Repository;

namespace Application.Implementacoes
{
    public class CategoriaService : ICategoriaService
    {
        private readonly ICategoriaRepository _categoriaRepository;
        private readonly IUsuarioLogado _usuarioLogado;

        public CategoriaService(ICategoriaRepository categoriaRepository, IUsuarioLogado usuarioLogado)
        {
            _categoriaRepository = categoriaRepository;
            _usuarioLogado = usuarioLogado;
        }

        public async Task<Result<ResultCategoriaDTO>> Adicionar(CreateCategoriaDTO categoriaDTO)
        {
            // Verificar permissão em modo compartilhado
            if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
                return Result.Failure<ResultCategoriaDTO>(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

            if (_categoriaRepository.CategoriaJaExiste(categoriaDTO.Nome, _usuarioLogado.IdContextoDados, categoriaDTO.Tipo.Value))
                return Result.Failure<ResultCategoriaDTO>(Error.Validation("Não e possivel criar categorias duplicadas!"));

            var categoria = new Categoria(categoriaDTO.Nome, categoriaDTO.Tipo.Value, _usuarioLogado.IdContextoDados);
            if (!string.IsNullOrWhiteSpace(categoriaDTO.McpOperationId))
                categoria.MarcarCriacaoMcp(categoriaDTO.McpOperationId);

            categoria = await _categoriaRepository.Add(categoria);

            return Result.Success(ResultCategoriaDTO.Mapear(categoria));
        }

        public async Task<Result<ResultCategoriaDTO>> Atualizar(UpdateCategoriaDTO categoriaDTO)
        {
            // Verificar permissão em modo compartilhado
            if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
                return Result.Failure<ResultCategoriaDTO>(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

            var categoria = await _categoriaRepository.GetById(categoriaDTO.Id);

            if (categoria is null || categoria.UsuarioId != _usuarioLogado.IdContextoDados)
                return Result.Failure<ResultCategoriaDTO>(Error.NotFound("Categoria informada não existe!"));

            categoria.AtualizarNome(categoriaDTO.Nome);

            await _categoriaRepository.Update(categoria);

            return Result.Success(ResultCategoriaDTO.Mapear(categoria));
        }

        public async Task<Result> Excluir(string id)
        {
            // Verificar permissão em modo compartilhado
            if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
                return Result.Failure(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

            var categoria = await _categoriaRepository.GetById(id);

            if (categoria is null || categoria.UsuarioId != _usuarioLogado.IdContextoDados)
                return Result.Failure(Error.NotFound("Categoria informada não existe!"));

            if (await _categoriaRepository.CategoriaPossuiVinculo(categoria))
                return Result.Failure(Error.Validation("Categoria informada possui vinculos realizados, por favor removas e após realize a exclusão!"));

            await _categoriaRepository.Delete(categoria);

            return Result.Success();
        }

        public async Task<Result<List<ResultCategoriaDTO>>> ObterCategoria(TipoCategoria tipoCategoria, string nome)
        {
            var categorias = await _categoriaRepository.GetCategorias(tipoCategoria, nome, _usuarioLogado.IdContextoDados);

            return Result.Success(categorias.Select(ResultCategoriaDTO.Mapear).ToList());
        }

        public async Task<Result<ResultCategoriaDTO>> ObterPeloID(string id)
        {
            var categoria = await _categoriaRepository.GetById(id);

            if (categoria is null || categoria.UsuarioId != _usuarioLogado.IdContextoDados)
                return Result.Failure<ResultCategoriaDTO>(Error.NotFound("Categoria informada não existe!"));

            return Result.Success(ResultCategoriaDTO.Mapear(categoria));
        }

        public async Task<McpApplicationMutationResult> AplicarMutacaoMcpAsync(
            McpWriteCommand command,
            string operationId,
            string resultHash,
            CancellationToken cancellationToken = default)
        {
            if (command.Entity != McpWriteEntity.Category ||
                command.TargetId is null ||
                command.ExpectedValues is null ||
                !TryCategory(command.ExpectedValues, out var expectedName, out var expectedType))
            {
                return MutationRejected(
                    "PREVIEW_PAYLOAD_INVALID",
                    "A prévia de categoria não contém o snapshot obrigatório.");
            }

            if (command.Action == Domain.Mcp.Enums.McpPreviewAction.Update)
            {
                var proposed = new Dictionary<string, object?>(command.ExpectedValues);
                foreach (var change in command.Values)
                    proposed[change.Key] = change.Value;
                if (!TryCategory(proposed, out var proposedName, out var proposedType))
                {
                    return MutationRejected(
                        "PREVIEW_PAYLOAD_INVALID",
                        "Os valores propostos para a categoria são inválidos.");
                }

                var updated = await _categoriaRepository.TryUpdateMcpAsync(
                    command.TargetId,
                    _usuarioLogado.IdContextoDados,
                    expectedName,
                    expectedType,
                    proposedName,
                    proposedType,
                    operationId,
                    resultHash,
                    cancellationToken);
                if (updated is not null)
                {
                    return MutationCompleted(
                        updated.Id,
                        operationId,
                        resultHash);
                }

                var current = await _categoriaRepository.GetById(command.TargetId);
                return current is null ||
                       current.UsuarioId != _usuarioLogado.IdContextoDados
                    ? MutationRejected(
                        "RECORD_NOT_FOUND",
                        "A categoria não foi encontrada para esta conta.")
                    : MutationConflict();
            }

            if (command.Action != Domain.Mcp.Enums.McpPreviewAction.Delete)
            {
                return MutationRejected(
                    "PREVIEW_PAYLOAD_INVALID",
                    "A ação MCP de categoria é inválida.");
            }

            var target = await _categoriaRepository.GetById(command.TargetId);
            if (target is null ||
                target.UsuarioId != _usuarioLogado.IdContextoDados)
            {
                return MutationRejected(
                    "RECORD_NOT_FOUND",
                    "A categoria não foi encontrada para esta conta.");
            }
            if (await _categoriaRepository.CategoriaPossuiVinculo(target))
            {
                return MutationRejected(
                    "DELETE_BLOCKED_RELATIONSHIP",
                    "A categoria possui registros vinculados. Remova ou altere esses vínculos antes de tentar excluí-la novamente.");
            }

            var deleted = await _categoriaRepository.TryDeleteMcpAsync(
                command.TargetId,
                _usuarioLogado.IdContextoDados,
                expectedName,
                expectedType,
                cancellationToken);
            if (deleted)
                return MutationCompleted(command.TargetId, operationId, resultHash);

            var observed = await _categoriaRepository.GetById(command.TargetId);
            return observed is null
                ? new McpApplicationMutationResult(
                    McpApplicationMutationState.Unknown,
                    command.TargetId,
                    null,
                    null,
                    "EFFECT_OUTCOME_UNKNOWN",
                    "A categoria está ausente, mas a causalidade da exclusão não pôde ser comprovada.")
                : MutationConflict();
        }

        private static bool TryCategory(
            IReadOnlyDictionary<string, object?> values,
            out string name,
            out TipoCategoria type)
        {
            name = values.GetValueOrDefault("name")?.ToString() ?? string.Empty;
            type = default;
            return !string.IsNullOrWhiteSpace(name) &&
                   Enum.TryParse(
                       values.GetValueOrDefault("type")?.ToString(),
                       true,
                       out type) &&
                   Enum.IsDefined(type);
        }

        private static McpApplicationMutationResult MutationCompleted(
            string id,
            string operationId,
            string resultHash) =>
            new(
                McpApplicationMutationState.Completed,
                id,
                operationId,
                resultHash,
                null,
                null);

        private static McpApplicationMutationResult MutationConflict() =>
            new(
                McpApplicationMutationState.ConflictChanged,
                null,
                null,
                null,
                "CONFLICT_CHANGED",
                "A categoria mudou desde a prévia; prepare uma nova.");

        private static McpApplicationMutationResult MutationRejected(
            string code,
            string message) =>
            new(
                McpApplicationMutationState.Rejected,
                null,
                null,
                null,
                code,
                message);
    }
}
