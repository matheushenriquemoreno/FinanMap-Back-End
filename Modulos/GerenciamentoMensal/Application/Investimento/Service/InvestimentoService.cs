using Application.DTOs;
using Application.Interface;
using Application.MetaFinanceira.DTOs;
using Application.MetaFinanceira.Interface;
using Application.Mcp.Models;
using Application.Shared.Transacao.DTOs;
using Domain.Compartilhamento.Entity;
using Domain.Entity;
using Domain.Enum;
using Domain.Login.Interfaces;
using Domain.Relatorios.AcumuladoMensal;
using Domain.Relatorios.Entity;
using Domain.Repository;
using System.Globalization;

#nullable enable annotations

namespace Application.Service;

public class InvestimentoService : IInvestimentoService
{
    private readonly IInvestimentoRepository _investimentoRepository;
    private readonly ICategoriaRepository _categoriaRepository;
    private readonly IAcumuladoMensalReportRepository _acumuladoMensalReportRepository;
    private readonly IUsuarioLogado _usuarioLogado;
    private readonly IMetaFinanceiraService _metaFinanceiraService;

    public InvestimentoService(
        IAcumuladoMensalReportRepository acumuladoMensalReportRepository,
        IInvestimentoRepository investimentoRepository,
        ICategoriaRepository categoriaRepository,
        IUsuarioLogado usuarioLogado,
        IMetaFinanceiraService metaFinanceiraService)
    {
        _acumuladoMensalReportRepository = acumuladoMensalReportRepository;
        _investimentoRepository = investimentoRepository;
        _categoriaRepository = categoriaRepository;
        _usuarioLogado = usuarioLogado;
        _metaFinanceiraService = metaFinanceiraService;
    }

    public async Task<Result<ResultInvestimentoDTO>> Adicionar(CreateInvestimentoDTO createDTO)
    {
        // Verificar permissão em modo compartilhado
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure<ResultInvestimentoDTO>(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        Categoria categoria = await _categoriaRepository.GetById(createDTO.CategoriaId);

        if (categoria == null)
            return Result.Failure<ResultInvestimentoDTO>(Error.NotFound("Categoria informada não existe!"));

        Investimento investimento = new(createDTO.Ano, createDTO.Mes, createDTO.Descricao, createDTO.Valor, categoria, _usuarioLogado.UsuarioContextoDados);
        if (!string.IsNullOrWhiteSpace(createDTO.McpOperationId))
            investimento.MarcarCriacaoMcp(createDTO.McpOperationId);

        await _investimentoRepository.Add(investimento);

        if (!string.IsNullOrEmpty(createDTO.MetaFinanceiraId))
        {
            var contribuicaoResult = await _metaFinanceiraService.AdicionarContribuicao(
                createDTO.MetaFinanceiraId,
                new ContribuicaoDTO
                {
                    Valor = investimento.Valor,
                    Data = DateTime.Now,
                    InvestimentoId = investimento.Id,
                    NomeInvestimento = investimento.Descricao
                });

            if (contribuicaoResult.IsFailure)
            {
                await _investimentoRepository.Delete(investimento);
                return Result.Failure<ResultInvestimentoDTO>(contribuicaoResult.Error);
            }
        }

        var reportAcumulado = await _acumuladoMensalReportRepository.Obter(investimento.Mes, investimento.Ano, _usuarioLogado.IdContextoDados);

        ResultInvestimentoDTO rendimentoDTO = ObterResultInvestimentoDTO(investimento, reportAcumulado);

        return Result.Success(rendimentoDTO);
    }

    public async Task<Result<ResultInvestimentoDTO>> Atualizar(UpdateInvestimentoDTO updateDTO)
    {
        // Verificar permissão em modo compartilhado
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure<ResultInvestimentoDTO>(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        Investimento investimento = await _investimentoRepository.GetById(updateDTO.Id);

        if (investimento == null)
            return Result.Failure<ResultInvestimentoDTO>(Error.NotFound("Investimento informado não existe!"));

        if (!PertenceAoContexto(investimento))
            return Result.Failure<ResultInvestimentoDTO>(Error.NotFound("Investimento informado não existe!"));

        var categoriaResult = await ObterCategoriaParaAtualizacao(updateDTO.CategoriaId, investimento.CategoriaId, TipoCategoria.Investimento);

        if (categoriaResult.IsFailure)
            return Result.Failure<ResultInvestimentoDTO>(categoriaResult.Error);

        investimento.Atualizar(updateDTO.Descricao, updateDTO.Valor, categoriaResult.Value);

        await _investimentoRepository.Update(investimento);

        var reportAcumulado = await _acumuladoMensalReportRepository.Obter(investimento.Mes, investimento.Ano, _usuarioLogado.IdContextoDados);

        return Result.Success(ObterResultInvestimentoDTO(investimento, reportAcumulado));
    }

    public async Task<Result> Excluir(string id)
    {
        // Verificar permissão em modo compartilhado
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var investimento = await _investimentoRepository.GetById(id);

        if (investimento == null)
            return Result.Failure(Error.NotFound("Investimento informado não existente"));

        await _investimentoRepository.Delete(investimento);

        return Result.Success();
    }

    public async Task<Result<ResultInvestimentoDTO>> ObterPeloID(string id)
    {
        var investimento = await _investimentoRepository.GetById(id);

        if (investimento == null)
            return Result.Failure<ResultInvestimentoDTO>(Error.NotFound("Investimento informado não existente"));

        return Result.Success(ObterResultInvestimentoDTO(investimento));
    }

    public async Task<List<ResultInvestimentoDTO>> ObterMesAno(int mes, int ano)
    {
        var despesas = await _investimentoRepository.ObterPeloMes(mes, ano, _usuarioLogado.IdContextoDados);

        return despesas.Select(x => ObterResultInvestimentoDTO(x)).ToList();
    }

    public async Task<Result<ResultInvestimentoDTO>> AtualizarValor(UpdateValorTransacaoDTO updateValorTransacaoDTO)
    {
        // Verificar permissão em modo compartilhado
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure<ResultInvestimentoDTO>(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var investimento = await _investimentoRepository.GetById(updateValorTransacaoDTO.Id);

        if (investimento == null)
            return Result.Failure<ResultInvestimentoDTO>(Error.NotFound("Investimento informado não existe!"));

        if (!PertenceAoContexto(investimento))
            return Result.Failure<ResultInvestimentoDTO>(Error.NotFound("Investimento informado não existe!"));

        investimento.AtualizarValor(updateValorTransacaoDTO.Valor);

        await _investimentoRepository.Update(investimento);

        var reportAcumulado = await _acumuladoMensalReportRepository.Obter(investimento.Mes, investimento.Ano, _usuarioLogado.IdContextoDados);

        return Result.Success(ObterResultInvestimentoDTO(investimento, reportAcumulado));
    }

    public async Task<McpApplicationMutationResult> AplicarMutacaoMcpAsync(
        McpWriteCommand command,
        string operationId,
        string resultHash,
        CancellationToken cancellationToken = default)
    {
        if (command.Entity != McpWriteEntity.Investment ||
            command.TargetId is null ||
            command.ExpectedValues is null ||
            !TryMcpSnapshot(command.ExpectedValues, out var expected))
            return McpRejected("PREVIEW_PAYLOAD_INVALID", "A prévia de investimento não contém o snapshot obrigatório.");

        if (command.Action == Domain.Mcp.Enums.McpPreviewAction.Update)
        {
            var values = new Dictionary<string, object?>(command.ExpectedValues);
            foreach (var change in command.Values)
                values[change.Key] = change.Value;
            if (!TryMcpSnapshot(values, out var proposed))
                return McpRejected("PREVIEW_PAYLOAD_INVALID", "Os valores propostos para o investimento são inválidos.");
            var category = await _categoriaRepository.GetById(proposed.CategoryId);
            if (category is null ||
                category.UsuarioId != _usuarioLogado.IdContextoDados ||
                category.Tipo != TipoCategoria.Investimento)
                return McpRejected("CATEGORY_RELATIONSHIP_INVALID", "A categoria proposta não existe nesta conta ou não aceita investimentos.");
            var updated = await _investimentoRepository.TryUpdateMcpAsync(
                command.TargetId,
                _usuarioLogado.IdContextoDados,
                expected,
                proposed,
                operationId,
                resultHash,
                cancellationToken);
            if (updated is not null)
                return McpCompleted(updated.Id, operationId, resultHash);
            var current = await _investimentoRepository.GetById(command.TargetId);
            return current is null || !PertenceAoContexto(current)
                ? McpRejected("RECORD_NOT_FOUND", "O investimento não foi encontrado para esta conta.")
                : McpConflict();
        }

        if (command.Action != Domain.Mcp.Enums.McpPreviewAction.Delete)
            return McpRejected("PREVIEW_PAYLOAD_INVALID", "A ação MCP de investimento é inválida.");
        var deleted = await _investimentoRepository.TryDeleteMcpAsync(
            command.TargetId,
            _usuarioLogado.IdContextoDados,
            expected,
            cancellationToken);
        if (deleted)
            return McpCompleted(command.TargetId, operationId, resultHash);
        var observed = await _investimentoRepository.GetById(command.TargetId);
        return observed is null
            ? new McpApplicationMutationResult(
                McpApplicationMutationState.Unknown,
                command.TargetId,
                null,
                null,
                "EFFECT_OUTCOME_UNKNOWN",
                "O investimento está ausente, mas a causalidade da exclusão não pôde ser comprovada.")
            : McpConflict();
    }

    private static bool TryMcpSnapshot(
        IReadOnlyDictionary<string, object?> values,
        out McpInvestmentSnapshot snapshot)
    {
        snapshot = default!;
        if (!int.TryParse(values.GetValueOrDefault("year")?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var year) ||
            !int.TryParse(values.GetValueOrDefault("month")?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var month) ||
            !decimal.TryParse(values.GetValueOrDefault("amount")?.ToString(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount))
            return false;
        var description = values.GetValueOrDefault("description")?.ToString() ?? string.Empty;
        var categoryId = values.GetValueOrDefault("categoryId")?.ToString() ?? string.Empty;
        if (year < DateTime.UtcNow.Year - 5 ||
            month is < 1 or > 12 ||
            amount <= 0 ||
            string.IsNullOrWhiteSpace(description) ||
            string.IsNullOrWhiteSpace(categoryId))
            return false;
        snapshot = new McpInvestmentSnapshot(year, month, description, amount, categoryId);
        return true;
    }

    private static McpApplicationMutationResult McpCompleted(string id, string operationId, string resultHash) =>
        new(McpApplicationMutationState.Completed, id, operationId, resultHash, null, null);

    private static McpApplicationMutationResult McpConflict() =>
        new(McpApplicationMutationState.ConflictChanged, null, null, null, "CONFLICT_CHANGED", "O investimento mudou desde a prévia; prepare uma nova.");

    private static McpApplicationMutationResult McpRejected(string code, string message) =>
        new(McpApplicationMutationState.Rejected, null, null, null, code, message);

    private ResultInvestimentoDTO ObterResultInvestimentoDTO(Investimento investimento, AcumuladoMensalReport? reportAcumulado = null)
    {
        var result = new ResultInvestimentoDTO()
        {
            Ano = investimento.Ano,
            Mes = investimento.Mes,
            CategoriaNome = investimento.Categoria?.Nome,
            CategoriaId = investimento.CategoriaId,
            Id = investimento.Id,
            Descricao = investimento.Descricao,
            Valor = investimento.Valor,
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
            return Result.Failure<Categoria>(Error.Validation("Categoria informada inválida para investimento."));

        return Result.Success(categoria);
    }

    private bool PertenceAoContexto(Investimento investimento)
    {
        return investimento.UsuarioId == _usuarioLogado.IdContextoDados;
    }
}

#nullable restore annotations
