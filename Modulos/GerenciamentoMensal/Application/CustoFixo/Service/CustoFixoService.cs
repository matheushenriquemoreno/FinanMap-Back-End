using Application.CustoFixo.DTOs;
using Application.CustoFixo.Interfaces;
using Application.Mcp.Models;
using Domain.Compartilhamento.Entity;
using Domain.Entity;
using Domain.Enum;
using Domain.Login.Interfaces;
using Domain.Repository;

#nullable enable annotations

namespace Application.CustoFixo.Service;

public class CustoFixoService : ICustoFixoService
{
    private readonly ICustoFixoRepository _custoFixoRepository;
    private readonly ICategoriaRepository _categoriaRepository;
    private readonly IUsuarioLogado _usuarioLogado;

    public CustoFixoService(
        ICustoFixoRepository custoFixoRepository,
        ICategoriaRepository categoriaRepository,
        IUsuarioLogado usuarioLogado)
    {
        _custoFixoRepository = custoFixoRepository;
        _categoriaRepository = categoriaRepository;
        _usuarioLogado = usuarioLogado;
    }

    public async Task<Result<CustoFixoResponseDTO>> Adicionar(CreateCustoFixoDTO createDTO)
    {
        if (!PodeEditar())
            return Result.Failure<CustoFixoResponseDTO>(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var categoriaResult = await ValidarCategoria(createDTO.CategoriaId);
        if (categoriaResult.IsFailure)
            return Result.Failure<CustoFixoResponseDTO>(categoriaResult.Error);

        if (await _custoFixoRepository.ExisteAtivoDuplicado(_usuarioLogado.IdContextoDados, createDTO.Nome, createDTO.DiaVencimento))
            return Result.Failure<CustoFixoResponseDTO>(Error.Validation("Ja existe um custo fixo ativo com esse nome e dia de vencimento."));

        var custoFixo = new Domain.Entity.CustoFixo(
            createDTO.Nome,
            createDTO.DiaVencimento,
            _usuarioLogado.IdContextoDados,
            createDTO.CategoriaId);
        if (!string.IsNullOrWhiteSpace(createDTO.McpOperationId))
            custoFixo.MarcarCriacaoMcp(createDTO.McpOperationId);

        await _custoFixoRepository.Add(custoFixo);

        return Result.Success(CustoFixoResponseDTO.Mapear(custoFixo, categoriaResult.Value?.Nome));
    }

    public async Task<Result<CustoFixoResponseDTO>> Atualizar(UpdateCustoFixoDTO updateDTO)
    {
        if (!PodeEditar())
            return Result.Failure<CustoFixoResponseDTO>(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var custoFixo = await ObterCustoFixoDoUsuario(updateDTO.Id);
        if (custoFixo is null)
            return Result.Failure<CustoFixoResponseDTO>(Error.NotFound("Custo fixo informado não existe!"));

        var categoriaResult = await ValidarCategoria(updateDTO.CategoriaId);
        if (categoriaResult.IsFailure)
            return Result.Failure<CustoFixoResponseDTO>(categoriaResult.Error);

        if (updateDTO.Ativo && await _custoFixoRepository.ExisteAtivoDuplicado(_usuarioLogado.IdContextoDados, updateDTO.Nome, updateDTO.DiaVencimento, updateDTO.Id))
            return Result.Failure<CustoFixoResponseDTO>(Error.Validation("Ja existe um custo fixo ativo com esse nome e dia de vencimento."));

        custoFixo.Atualizar(updateDTO.Nome, updateDTO.DiaVencimento, updateDTO.CategoriaId, updateDTO.Ativo);

        await _custoFixoRepository.Update(custoFixo);

        return Result.Success(CustoFixoResponseDTO.Mapear(custoFixo, categoriaResult.Value?.Nome));
    }

    public async Task<Result> Excluir(string id)
    {
        if (!PodeEditar())
            return Result.Failure(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var custoFixo = await ObterCustoFixoDoUsuario(id);
        if (custoFixo is null)
            return Result.Failure(Error.NotFound("Custo fixo informado não existe!"));

        await _custoFixoRepository.Delete(custoFixo);

        return Result.Success();
    }

    public async Task<Result<List<CustoFixoResponseDTO>>> Listar()
    {
        var custosFixos = await _custoFixoRepository.GetByUsuarioId(_usuarioLogado.IdContextoDados);
        var categoriasPorId = await ObterCategoriasPorId(custosFixos);

        return Result.Success(custosFixos
            .Select(custoFixo => CustoFixoResponseDTO.Mapear(
                custoFixo,
                ObterNomeCategoria(custoFixo.CategoriaId, categoriasPorId)))
            .ToList());
    }

    public async Task<Result<CustoFixoResponseDTO>> ObterPeloID(string id)
    {
        var custoFixo = await ObterCustoFixoDoUsuario(id);
        if (custoFixo is null)
            return Result.Failure<CustoFixoResponseDTO>(Error.NotFound("Custo fixo informado não existe!"));

        var categoriaNome = await ObterNomeCategoria(custoFixo.CategoriaId);

        return Result.Success(CustoFixoResponseDTO.Mapear(custoFixo, categoriaNome));
    }

    public async Task<McpApplicationMutationResult> AplicarMutacaoMcpAsync(
        McpWriteCommand command,
        string operationId,
        string resultHash,
        CancellationToken cancellationToken = default)
    {
        if (command.Entity != McpWriteEntity.FixedCost ||
            command.TargetId is null ||
            command.ExpectedValues is null ||
            !TryMcpSnapshot(command.ExpectedValues, out var expected))
            return McpRejected("PREVIEW_PAYLOAD_INVALID", "A prévia de custo fixo não contém o snapshot obrigatório.");

        if (command.Action == Domain.Mcp.Enums.McpPreviewAction.Update)
        {
            var values = new Dictionary<string, object?>(command.ExpectedValues);
            foreach (var change in command.Values)
                values[change.Key] = change.Value;
            if (!TryMcpSnapshot(values, out var proposed))
                return McpRejected("PREVIEW_PAYLOAD_INVALID", "Os valores propostos para o custo fixo são inválidos.");
            var category = await ValidarCategoria(proposed.CategoryId);
            if (category.IsFailure)
                return McpRejected("CATEGORY_RELATIONSHIP_INVALID", "A categoria proposta não existe nesta conta ou não aceita custos fixos.");
            if (proposed.Active &&
                await _custoFixoRepository.ExisteAtivoDuplicado(
                    _usuarioLogado.IdContextoDados,
                    proposed.Name,
                    proposed.DueDay,
                    command.TargetId))
                return McpRejected("DOMAIN_DUPLICATE", "Já existe um custo fixo ativo com esse nome e dia de vencimento.");
            var updated = await _custoFixoRepository.TryUpdateMcpAsync(
                command.TargetId,
                _usuarioLogado.IdContextoDados,
                expected,
                proposed,
                operationId,
                resultHash,
                cancellationToken);
            if (updated is not null)
                return McpCompleted(updated.Id, operationId, resultHash);
            var current = await _custoFixoRepository.GetById(command.TargetId);
            return current is null || current.UsuarioId != _usuarioLogado.IdContextoDados
                ? McpRejected("RECORD_NOT_FOUND", "O custo fixo não foi encontrado para esta conta.")
                : McpConflict();
        }

        if (command.Action != Domain.Mcp.Enums.McpPreviewAction.Delete)
            return McpRejected("PREVIEW_PAYLOAD_INVALID", "A ação MCP de custo fixo é inválida.");
        var deleted = await _custoFixoRepository.TryDeleteMcpAsync(
            command.TargetId,
            _usuarioLogado.IdContextoDados,
            expected,
            cancellationToken);
        if (deleted)
            return McpCompleted(command.TargetId, operationId, resultHash);
        var observed = await _custoFixoRepository.GetById(command.TargetId);
        return observed is null
            ? new McpApplicationMutationResult(
                McpApplicationMutationState.Unknown,
                command.TargetId,
                null,
                null,
                "EFFECT_OUTCOME_UNKNOWN",
                "O custo fixo está ausente, mas a causalidade da exclusão não pôde ser comprovada.")
            : McpConflict();
    }

    private static bool TryMcpSnapshot(
        IReadOnlyDictionary<string, object?> values,
        out McpFixedCostSnapshot snapshot)
    {
        snapshot = default!;
        var name = values.GetValueOrDefault("name")?.ToString() ?? string.Empty;
        if (!int.TryParse(values.GetValueOrDefault("dueDay")?.ToString(), out var dueDay) ||
            !bool.TryParse(values.GetValueOrDefault("active")?.ToString(), out var active) ||
            string.IsNullOrWhiteSpace(name) ||
            dueDay is < 1 or > 31)
            return false;
        snapshot = new McpFixedCostSnapshot(
            name,
            dueDay,
            values.GetValueOrDefault("categoryId")?.ToString(),
            active);
        return true;
    }

    private static McpApplicationMutationResult McpCompleted(string id, string operationId, string resultHash) =>
        new(McpApplicationMutationState.Completed, id, operationId, resultHash, null, null);

    private static McpApplicationMutationResult McpConflict() =>
        new(McpApplicationMutationState.ConflictChanged, null, null, null, "CONFLICT_CHANGED", "O custo fixo mudou desde a prévia; prepare uma nova.");

    private static McpApplicationMutationResult McpRejected(string code, string message) =>
        new(McpApplicationMutationState.Rejected, null, null, null, code, message);

    private bool PodeEditar()
    {
        return !_usuarioLogado.EmModoCompartilhado || _usuarioLogado.PermissaoAtual == NivelPermissao.Editar;
    }

    private async Task<Domain.Entity.CustoFixo> ObterCustoFixoDoUsuario(string id)
    {
        var custoFixo = await _custoFixoRepository.GetById(id);

        if (custoFixo is null || custoFixo.UsuarioId != _usuarioLogado.IdContextoDados)
            return null;

        return custoFixo;
    }

    private async Task<Result<Categoria>> ValidarCategoria(string categoriaId)
    {
        if (string.IsNullOrWhiteSpace(categoriaId))
            return Result.Success<Categoria>(null);

        Categoria categoria = await _categoriaRepository.GetById(categoriaId);

        if (categoria is null || categoria.UsuarioId != _usuarioLogado.IdContextoDados || categoria.Tipo != TipoCategoria.Despesa)
            return Result.Failure<Categoria>(Error.NotFound("Categoria informada não existe!"));

        return Result.Success(categoria);
    }

    private async Task<Dictionary<string, string>> ObterCategoriasPorId(List<Domain.Entity.CustoFixo> custosFixos)
    {
        var categoriaIds = custosFixos
            .Where(custoFixo => !string.IsNullOrWhiteSpace(custoFixo.CategoriaId))
            .Select(custoFixo => custoFixo.CategoriaId)
            .Distinct()
            .ToList();

        if (!categoriaIds.Any())
            return new Dictionary<string, string>();

        var categorias = await _categoriaRepository.GetByIds(categoriaIds);

        return categorias
            .Where(categoria => categoria.UsuarioId == _usuarioLogado.IdContextoDados && categoria.Tipo == TipoCategoria.Despesa)
            .ToDictionary(categoria => categoria.Id, categoria => categoria.Nome);
    }

    private async Task<string> ObterNomeCategoria(string categoriaId)
    {
        if (string.IsNullOrWhiteSpace(categoriaId))
            return null;

        var categoria = await _categoriaRepository.GetById(categoriaId);

        if (categoria is null || categoria.UsuarioId != _usuarioLogado.IdContextoDados || categoria.Tipo != TipoCategoria.Despesa)
            return null;

        return categoria.Nome;
    }

    private static string ObterNomeCategoria(string categoriaId, Dictionary<string, string> categoriasPorId)
    {
        if (string.IsNullOrWhiteSpace(categoriaId))
            return null;

        return categoriasPorId.GetValueOrDefault(categoriaId);
    }
}

#nullable restore annotations
