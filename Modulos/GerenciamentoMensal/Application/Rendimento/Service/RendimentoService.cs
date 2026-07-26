using Application.DTOs;
using System.Globalization;
using Application.Interface;
using Application.Mcp.Models;
using Application.Shared.Transacao.DTOs;
using Domain.Compartilhamento.Entity;
using Domain.Entity;
using Domain.Enum;
using Domain.Login.Interfaces;
using Domain.Relatorios.AcumuladoMensal;
using Domain.Relatorios.Entity;
using Domain.Repository;
using Mapster;

namespace Application.Service;

public class RendimentoService : IRendimentoService
{
    private readonly IRendimentoRepository _rendimentoRepository;
    private readonly ICategoriaRepository _categoriaRepository;
    private readonly IAcumuladoMensalReportRepository _acumuladoMensalReportRepository;
    private readonly IUsuarioLogado _usuarioLogado;

    public RendimentoService(IRendimentoRepository repository, ICategoriaRepository categoriaRepository, IAcumuladoMensalReportRepository acumuladoMensalReportRepository, IUsuarioLogado usuarioLogado)
    {
        _rendimentoRepository = repository;
        _categoriaRepository = categoriaRepository;
        _acumuladoMensalReportRepository = acumuladoMensalReportRepository;
        _usuarioLogado = usuarioLogado;
    }

    public async Task<Result<ResultRendimentoDTO>> Adicionar(CreateRendimentoDTO createDTO)
    {
        // Verificar permissão de edição em modo compartilhado
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure<ResultRendimentoDTO>(
                Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        Categoria? categoria = await _categoriaRepository.GetById(createDTO.CategoriaId);

        if (categoria == null ||
            categoria.UsuarioId != _usuarioLogado.IdContextoDados ||
            categoria.Tipo != TipoCategoria.Rendimento)
            return Result.Failure<ResultRendimentoDTO>(Error.NotFound("Categoria informada não existe!"));

        Rendimento rendimento = new Rendimento(createDTO.Ano, createDTO.Mes, createDTO.Descricao, createDTO.Valor, categoria, _usuarioLogado.UsuarioContextoDados);
        if (!string.IsNullOrWhiteSpace(createDTO.McpOperationId))
            rendimento.MarcarCriacaoMcp(createDTO.McpOperationId);

        await _rendimentoRepository.Add(rendimento);

        var reportAcumulado = await _acumuladoMensalReportRepository.Obter(rendimento.Mes, rendimento.Ano, _usuarioLogado.IdContextoDados);

        ResultRendimentoDTO rendimentoDTO = ObterRendimentoDTO(rendimento, reportAcumulado);

        return Result.Success(rendimentoDTO);
    }

    public async Task<Result<ResultRendimentoDTO>> Atualizar(UpdateRendimentoDTO updateDTO)
    {
        // Verificar permissão de edição em modo compartilhado
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure<ResultRendimentoDTO>(
                Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        Rendimento rendimento = await _rendimentoRepository.GetById(updateDTO.Id);

        if (rendimento == null || !PertenceAoContexto(rendimento))
            return Result.Failure<ResultRendimentoDTO>(Error.NotFound("Rendimento informado não existe!"));

        if (!PertenceAoContexto(rendimento))
            return Result.Failure<ResultRendimentoDTO>(Error.NotFound("Rendimento informado não existe!"));

        var categoriaResult = await ObterCategoriaParaAtualizacao(updateDTO.CategoriaId, rendimento.CategoriaId, TipoCategoria.Rendimento);

        if (categoriaResult.IsFailure)
            return Result.Failure<ResultRendimentoDTO>(categoriaResult.Error);

        rendimento.Atualizar(updateDTO.Descricao, updateDTO.Valor, categoriaResult.Value);

        await _rendimentoRepository.Update(rendimento);

        var reportAcumulado = await _acumuladoMensalReportRepository.Obter(rendimento.Mes, rendimento.Ano, _usuarioLogado.IdContextoDados);

        ResultRendimentoDTO rendimentoDTO = ObterRendimentoDTO(rendimento, reportAcumulado);

        return Result.Success(rendimentoDTO);
    }

    public async Task<Result> Excluir(string id)
    {
        // Verificar permissão de edição em modo compartilhado
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure(
                Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var rendimento = await _rendimentoRepository.GetById(id);

        if (rendimento == null || !PertenceAoContexto(rendimento))
            return Result.Failure(Error.NotFound("Rendimento informado não existe!"));

        await _rendimentoRepository.Delete(rendimento);

        return Result.Success();
    }

    public async Task<Result<ResultRendimentoDTO>> ObterPeloID(string id)
    {
        var rendimento = await _rendimentoRepository.GetById(id);

        if (rendimento == null || !PertenceAoContexto(rendimento))
            return Result.Failure<ResultRendimentoDTO>(Error.NotFound("Rendimento informado não existe!"));

        return Result.Success(rendimento.Adapt<ResultRendimentoDTO>());
    }

    public async Task<List<ResultRendimentoDTO>> ObterRendimentoMes(int mes, int ano)
    {
        var rendimentos = await _rendimentoRepository.ObterPeloMes(mes, ano, _usuarioLogado.IdContextoDados);

        return rendimentos.Select(x => ObterRendimentoDTO(x)).ToList();
    }

    public async Task<Result<ResultRendimentoDTO>> AtualizarValor(UpdateValorTransacaoDTO updateValorTransacaoDTO)
    {
        // Verificar permissão de edição em modo compartilhado
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure<ResultRendimentoDTO>(
                Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        Rendimento rendimento = await _rendimentoRepository.GetById(updateValorTransacaoDTO.Id);

        if (rendimento == null || !PertenceAoContexto(rendimento))
            return Result.Failure<ResultRendimentoDTO>(Error.NotFound("Rendimento informado não existe!"));

        if (!PertenceAoContexto(rendimento))
            return Result.Failure<ResultRendimentoDTO>(Error.NotFound("Rendimento informado não existe!"));

        rendimento.AtualizarValor(updateValorTransacaoDTO.Valor);

        await _rendimentoRepository.Update(rendimento);

        var reportAcumulado = await _acumuladoMensalReportRepository.Obter(rendimento.Mes, rendimento.Ano, _usuarioLogado.IdContextoDados);

        return Result.Success(ObterRendimentoDTO(rendimento, reportAcumulado));
    }


    public async Task<McpApplicationMutationResult> AplicarMutacaoMcpAsync(
        McpWriteCommand command,
        string operationId,
        string resultHash,
        CancellationToken cancellationToken = default)
    {
        if (command.Entity != McpWriteEntity.Income ||
            command.TargetId is null ||
            command.ExpectedValues is null ||
            !TryIncome(
                command.ExpectedValues,
                out var expectedYear,
                out var expectedMonth,
                out var expectedDescription,
                out var expectedAmount,
                out var expectedCategoryId))
        {
            return MutationRejected(
                "PREVIEW_PAYLOAD_INVALID",
                "A prévia de receita não contém o snapshot obrigatório.");
        }

        if (command.Action == Domain.Mcp.Enums.McpPreviewAction.Update)
        {
            var proposed = new Dictionary<string, object?>(command.ExpectedValues);
            foreach (var change in command.Values)
                proposed[change.Key] = change.Value;
            if (!TryIncome(
                    proposed,
                    out _,
                    out _,
                    out var proposedDescription,
                    out var proposedAmount,
                    out var proposedCategoryId))
            {
                return MutationRejected(
                    "PREVIEW_PAYLOAD_INVALID",
                    "Os valores propostos para a receita são inválidos.");
            }

            var category = await _categoriaRepository.GetById(proposedCategoryId);
            if (category is null ||
                category.UsuarioId != _usuarioLogado.IdContextoDados ||
                category.Tipo != TipoCategoria.Rendimento)
            {
                return MutationRejected(
                    "CATEGORY_RELATIONSHIP_INVALID",
                    "A categoria proposta não existe nesta conta ou não aceita receitas.");
            }

            var updated = await _rendimentoRepository.TryUpdateMcpAsync(
                command.TargetId,
                _usuarioLogado.IdContextoDados,
                expectedYear,
                expectedMonth,
                expectedDescription,
                expectedAmount,
                expectedCategoryId,
                proposedDescription,
                proposedAmount,
                proposedCategoryId,
                operationId,
                resultHash,
                cancellationToken);
            if (updated is not null)
                return MutationCompleted(updated.Id, operationId, resultHash);

            var current = await _rendimentoRepository.GetById(command.TargetId);
            return current is null || !PertenceAoContexto(current)
                ? MutationRejected(
                    "RECORD_NOT_FOUND",
                    "A receita não foi encontrada para esta conta.")
                : MutationConflict();
        }

        if (command.Action != Domain.Mcp.Enums.McpPreviewAction.Delete)
        {
            return MutationRejected(
                "PREVIEW_PAYLOAD_INVALID",
                "A ação MCP de receita é inválida.");
        }

        var deleted = await _rendimentoRepository.TryDeleteMcpAsync(
            command.TargetId,
            _usuarioLogado.IdContextoDados,
            expectedYear,
            expectedMonth,
            expectedDescription,
            expectedAmount,
            expectedCategoryId,
            cancellationToken);
        if (deleted)
            return MutationCompleted(command.TargetId, operationId, resultHash);

        var observed = await _rendimentoRepository.GetById(command.TargetId);
        return observed is null
            ? new McpApplicationMutationResult(
                McpApplicationMutationState.Unknown,
                command.TargetId,
                null,
                null,
                "EFFECT_OUTCOME_UNKNOWN",
                "A receita está ausente, mas a causalidade da exclusão não pôde ser comprovada.")
            : MutationConflict();
    }

    private static bool TryIncome(
        IReadOnlyDictionary<string, object?> values,
        out int year,
        out int month,
        out string description,
        out decimal amount,
        out string categoryId)
    {
        year = 0;
        month = 0;
        amount = 0;
        description = values.GetValueOrDefault("description")?.ToString() ?? string.Empty;
        categoryId = values.GetValueOrDefault("categoryId")?.ToString() ?? string.Empty;
        return int.TryParse(
                   values.GetValueOrDefault("year")?.ToString(),
                   NumberStyles.Integer,
                   CultureInfo.InvariantCulture,
                   out year) &&
               int.TryParse(
                   values.GetValueOrDefault("month")?.ToString(),
                   NumberStyles.Integer,
                   CultureInfo.InvariantCulture,
                   out month) &&
               decimal.TryParse(
                   values.GetValueOrDefault("amount")?.ToString(),
                   NumberStyles.AllowDecimalPoint,
                   CultureInfo.InvariantCulture,
                   out amount) &&
               year >= DateTime.UtcNow.Year - 5 &&
               month is >= 1 and <= 12 &&
               amount > 0 &&
               !string.IsNullOrWhiteSpace(description) &&
               !string.IsNullOrWhiteSpace(categoryId);
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
            "A receita mudou desde a prévia; prepare uma nova.");

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

    #region metodos privado
    private static ResultRendimentoDTO ObterRendimentoDTO(Rendimento rendimento, AcumuladoMensalReport? reportAcumulado = null)
    {
        var result = new ResultRendimentoDTO()
        {
            Ano = rendimento.Ano,
            Mes = rendimento.Mes,
            CategoriaNome = rendimento.Categoria?.Nome,
            CategoriaId = rendimento.Categoria?.Id,
            Id = rendimento.Id,
            Descricao = rendimento.Descricao,
            Valor = rendimento.Valor,
            ReportAcumulado = reportAcumulado
        };

        return result;
    }

    private async Task<Result<Categoria>> ObterCategoriaParaAtualizacao(string categoriaIdInformada, string categoriaIdAtual, TipoCategoria tipoEsperado)
    {
        var categoriaId = string.IsNullOrWhiteSpace(categoriaIdInformada)
            ? categoriaIdAtual
            : categoriaIdInformada;

        var categoria = await _categoriaRepository.GetById(categoriaId);

        if (categoria == null || categoria.UsuarioId != _usuarioLogado.IdContextoDados)
            return Result.Failure<Categoria>(Error.NotFound("Categoria informada não existe!"));

        if (categoria.Tipo != tipoEsperado)
            return Result.Failure<Categoria>(Error.Validation("Categoria informada inválida para rendimento."));

        return Result.Success(categoria);
    }

    private bool PertenceAoContexto(Rendimento rendimento)
    {
        return rendimento.UsuarioId == _usuarioLogado.IdContextoDados;
    }
    #endregion
}
