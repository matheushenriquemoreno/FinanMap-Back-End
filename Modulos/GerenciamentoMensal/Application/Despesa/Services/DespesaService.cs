using Application.DTOs;
using Application.Interfaces;
using Application.Mcp.Models;
using Application.Shared.Transacao.DTOs;
using Domain.Compartilhamento.Entity;
using Domain.Entity;
using Domain.Enum;
using Domain.Enums;
using Domain.Login.Interfaces;
using Domain.Relatorios.AcumuladoMensal;
using Domain.Relatorios.Entity;
using Domain.Repository;
using System.Globalization;
using System.Text.RegularExpressions;

#nullable enable annotations

namespace Application.Services;

public class DespesaService : IDespesaService
{
    private readonly IDespesaRepository _repository;
    private readonly ICategoriaRepository _categoriaRepository;
    private readonly IAcumuladoMensalReportRepository _acumuladoMensalReportRepository;
    private readonly IUsuarioLogado _usuarioLogado;
    private readonly DespesaAgrupamentoService _agrupamentoService;

    public DespesaService(IDespesaRepository repository, ICategoriaRepository categoriaRepository, IAcumuladoMensalReportRepository acumuladoMensalReportRepository, IUsuarioLogado usuarioLogado)
    {
        _repository = repository;
        _categoriaRepository = categoriaRepository;
        _acumuladoMensalReportRepository = acumuladoMensalReportRepository;
        _usuarioLogado = usuarioLogado;
        _agrupamentoService = new DespesaAgrupamentoService(repository);
    }

    public async Task<Result<ResultDespesaDTO>> Adicionar(CreateDespesaDTO createDTO)
    {
        // Verificar permissão em modo compartilhado
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure<ResultDespesaDTO>(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        Categoria? categoria = await _categoriaRepository.GetById(createDTO.CategoriaId);

        if (categoria == null)
            return Result.Failure<ResultDespesaDTO>(Error.NotFound("Categoria informada não existe!"));

        Despesa despesa = new Despesa(createDTO.Ano, createDTO.Mes, createDTO.Descricao, createDTO.Valor, categoria, _usuarioLogado.UsuarioContextoDados);
        despesa.DespesaOrigemId = createDTO.DespesaOrigemId;
        despesa.IsParcelado = createDTO.IsParcelado;
        despesa.IsRecorrente = createDTO.IsRecorrente;
        despesa.ParcelaAtual = createDTO.ParcelaAtual;
        despesa.TotalParcelas = createDTO.TotalParcelas;
        if (!string.IsNullOrWhiteSpace(createDTO.McpOperationId))
            despesa.MarcarCriacaoMcp(createDTO.McpOperationId);

        if (!string.IsNullOrEmpty(createDTO.IdDespesaAgrupadora))
        {
            var despesaAgrupadora = await _repository.GetById(createDTO.IdDespesaAgrupadora);
            if (despesaAgrupadora == null)
                return Result.Failure<ResultDespesaDTO>(Error.NotFound("Despesa para realizar agrupamento não existe!"));

            var contextoAgrupadora = await _agrupamentoService.CapturarContextoAsync(despesaAgrupadora.Id);
            _agrupamentoService.Vincular(despesa, despesaAgrupadora);
            if (string.IsNullOrWhiteSpace(createDTO.McpOperationId))
                await _agrupamentoService.SincronizarAgrupadoraComFilhasPendentesAsync(contextoAgrupadora, [despesa]);
        }

        await _repository.Add(despesa);

        var reportAcumulado = await _acumuladoMensalReportRepository.Obter(despesa.Mes, despesa.Ano, _usuarioLogado.IdContextoDados);

        ResultDespesaDTO rendimentoDTO = ObterDespesaDTO(despesa, reportAcumulado);

        return Result.Success(rendimentoDTO);
    }

    public async Task<Result<ResultDespesaDTO>> Atualizar(UpdateDespesaDTO updateDTO)
    {
        // Verificar permissão em modo compartilhado
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure<ResultDespesaDTO>(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var despesa = await _repository.GetById(updateDTO.Id);

        if (despesa == null)
            return Result.Failure<ResultDespesaDTO>(Error.NotFound("Despesa informada não existente"));

        Categoria categoria = await _categoriaRepository.GetById(updateDTO.CategoriaId);

        if (categoria == null)
            return Result.Failure<ResultDespesaDTO>(Error.NotFound("Categoria informada não existe!"));

        if (despesa.EhAgrupadora())
        {
            if (updateDTO.IdDespesaAgrupadora.PossuiValor())
                return Result.Failure<ResultDespesaDTO>(Error.NotFound("Despesa que e usada para agrupar outras, não pode ser vinculada a nenhuma outra."));

            var valorAgrupamento = await _repository.GetValorTotalDespesasDaAgrupadora(despesa.Id);

            if (valorAgrupamento > updateDTO.Valor)
                return Result.Failure<ResultDespesaDTO>(Error.Validation($"Valor informado menor que o total das despesas agrupadas, valor total: {valorAgrupamento}"));
        }

        var contextosAgrupadoras = new Dictionary<string, DespesaAgrupamentoContexto>();
        await CapturarAgrupadoraAfetada(contextosAgrupadoras, despesa.IdDespesaAgrupadora);

        var resultAgrupamento = await AtualizarAgrupamento(despesa, updateDTO, contextosAgrupadoras);
        if (resultAgrupamento.IsFailure)
            return Result.Failure<ResultDespesaDTO>(resultAgrupamento.Error);

        despesa.Descricao = updateDTO.Descricao;
        despesa.AtualizarValor(updateDTO.Valor);
        despesa.PreencherCategoria(categoria);

        await _repository.Update(despesa);

        await SincronizarAgrupadoras(contextosAgrupadoras.Values);

        var reportAcumulado = await _acumuladoMensalReportRepository.Obter(despesa.Mes, despesa.Ano, _usuarioLogado.IdContextoDados);

        ResultDespesaDTO rendimentoDTO = ObterDespesaDTO(despesa, reportAcumulado);

        return Result.Success(rendimentoDTO);
    }

    private async Task<Result> AtualizarAgrupamento(
        Despesa despesa,
        UpdateDespesaDTO updateDTO,
        Dictionary<string, DespesaAgrupamentoContexto> contextosAgrupadoras)
    {
        // Cenario de remover vinculo com a despesa agrupadora
        if (despesa.EstaAgrupada() &&
            updateDTO.IdDespesaAgrupadora.PossuiValor() == false)
        {
            _agrupamentoService.RemoverVinculo(despesa);
            return Result.Success();
        }

        // cenario de agrupamento após a criação da despesa
        if (updateDTO.IdDespesaAgrupadora.PossuiValor() &&
                despesa.EstaAgrupada() == false)
        {
            var despesaAgrupadora = await _repository.GetById(updateDTO.IdDespesaAgrupadora);
            if (despesaAgrupadora == null)
                return Result.Failure(Error.NotFound("Despesa para realizar agrupamento não existe!"));

            await CapturarAgrupadoraAfetada(contextosAgrupadoras, despesaAgrupadora.Id);
            _agrupamentoService.Vincular(despesa, despesaAgrupadora);
            return Result.Success();
        }

        // cenario de troca de agrupamento, era de uma despesa foi para outra.
        if (updateDTO.IdDespesaAgrupadora.PossuiValor() &&
                despesa.IdDespesaAgrupadora != updateDTO.IdDespesaAgrupadora)
        {
            _agrupamentoService.RemoverVinculo(despesa);

            var despesaAgrupadora = await _repository.GetById(updateDTO.IdDespesaAgrupadora);
            if (despesaAgrupadora == null)
                return Result.Failure(Error.NotFound("Despesa para realizar agrupamento não existe!"));

            await CapturarAgrupadoraAfetada(contextosAgrupadoras, despesaAgrupadora.Id);
            _agrupamentoService.Vincular(despesa, despesaAgrupadora);

            return Result.Success();
        }

        return Result.Success();
    }

    public async Task<Result> Excluir(string id)
    {
        // Verificar permissão em modo compartilhado
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        Despesa despesa = await _repository.GetById(id);

        if (despesa == null)
            return Result.Failure(Error.NotFound("Despesa informada não existente"));

        if (despesa.EstaAgrupada())
        {
            var contextoAgrupadora = await _agrupamentoService.CapturarContextoAsync(despesa.IdDespesaAgrupadora);
            await _repository.Delete(despesa);
            await _agrupamentoService.SincronizarAgrupadoraAsync(contextoAgrupadora);
            return Result.Success();
        }
        else if (despesa.EhAgrupadora())
        {
            var despesasFilhas = await _repository.GetDespesasDaAgrupadora(despesa.Id);

            foreach (var filha in despesasFilhas)
            {
                await _repository.Delete(filha);
            }
        }

        await _repository.Delete(despesa);

        return Result.Success();
    }

    public async Task<Result<ResultDespesaDTO>> ObterPeloID(string id)
    {
        var despesa = await _repository.GetById(id);

        if (despesa == null)
            return Result.Failure<ResultDespesaDTO>(Error.NotFound("Despesa informada não existente"));

        return Result.Success(ObterDespesaDTO(despesa));
    }

    public async Task<List<ResultDespesaDTO>> ObterMesAno(int mes, int ano, string descricao)
    {
        var despesas = await _repository.GetPeloMes(mes, ano, _usuarioLogado.IdContextoDados, descricao);

        return despesas.Select(x => ObterDespesaDTO(x)).ToList();
    }

    public async Task<List<ResultDespesaDTO>> ObterDespesasDaAgrupadora(string idDespesa)
    {
        var despesas = await _repository.GetDespesasDaAgrupadora(idDespesa);

        return despesas.Select(x => ObterDespesaDTO(x)).ToList();
    }

    public async Task<Result<ResultDespesaDTO>> AtualizarValor(UpdateValorTransacaoDTO updateValorTransacaoDTO)
    {
        // Verificar permissão em modo compartilhado
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure<ResultDespesaDTO>(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var despesa = await _repository.GetById(updateValorTransacaoDTO.Id);

        if (despesa == null)
            return Result.Failure<ResultDespesaDTO>(Error.NotFound("Despesa informado não existe!"));

        if (despesa.EhAgrupadora())
        {
            var valorAgrupamento = await _repository.GetValorTotalDespesasDaAgrupadora(despesa.Id);

            if (valorAgrupamento > updateValorTransacaoDTO.Valor)
                return Result.Failure<ResultDespesaDTO>(Error.Validation($"Valor informado menor que o total das despesas agrupadas, valor total: {valorAgrupamento}"));
        }

        var contextoAgrupadora = despesa.EstaAgrupada()
            ? await _agrupamentoService.CapturarContextoAsync(despesa.IdDespesaAgrupadora)
            : null;

        despesa.AtualizarValor(updateValorTransacaoDTO.Valor);

        await _repository.Update(despesa);

        await _agrupamentoService.SincronizarAgrupadoraAsync(contextoAgrupadora);
        if (contextoAgrupadora != null)
            despesa.Agrupadora = contextoAgrupadora.Agrupadora;

        var reportAcumulado = await _acumuladoMensalReportRepository.Obter(despesa.Mes, despesa.Ano, _usuarioLogado.IdContextoDados);

        return Result.Success(ObterDespesaDTO(despesa, reportAcumulado));
    }

    public async Task<McpApplicationMutationResult> AplicarMutacaoMcpAsync(
        McpWriteCommand command,
        string operationId,
        string resultHash,
        CancellationToken cancellationToken = default)
    {
        if (command.Entity != McpWriteEntity.Expense ||
            command.TargetId is null ||
            command.ExpectedValues is null ||
            !TryMcpSnapshot(command.ExpectedValues, out var expected))
            return McpRejected("PREVIEW_PAYLOAD_INVALID", "A prévia de despesa não contém o snapshot obrigatório.");

        if (command.Action == Domain.Mcp.Enums.McpPreviewAction.Update)
        {
            var values = new Dictionary<string, object?>(command.ExpectedValues);
            foreach (var change in command.Values)
                values[change.Key] = change.Value;
            if (!TryMcpSnapshot(values, out var proposed))
                return McpRejected("PREVIEW_PAYLOAD_INVALID", "Os valores propostos para a despesa são inválidos.");

            var category = await _categoriaRepository.GetById(proposed.CategoryId);
            if (category is null ||
                category.UsuarioId != _usuarioLogado.IdContextoDados ||
                category.Tipo != TipoCategoria.Despesa)
                return McpRejected("CATEGORY_RELATIONSHIP_INVALID", "A categoria proposta não existe nesta conta ou não aceita despesas.");

            if (!string.IsNullOrWhiteSpace(proposed.GroupingExpenseId))
            {
                var grouping = await _repository.GetById(proposed.GroupingExpenseId);
                if (grouping is null ||
                    grouping.UsuarioId != _usuarioLogado.IdContextoDados ||
                    grouping.Id == command.TargetId)
                    return McpRejected("GROUPING_RELATIONSHIP_INVALID", "A despesa agrupadora não existe nesta conta ou é inválida.");
            }

            var updated = await _repository.TryUpdateMcpAsync(
                command.TargetId,
                _usuarioLogado.IdContextoDados,
                expected,
                proposed,
                operationId,
                resultHash,
                cancellationToken);
            if (updated is not null)
                return McpCompleted(updated.Id, operationId, resultHash);

            var current = await _repository.GetById(command.TargetId);
            return current is null || current.UsuarioId != _usuarioLogado.IdContextoDados
                ? McpRejected("RECORD_NOT_FOUND", "A despesa não foi encontrada para esta conta.")
                : McpConflict("A despesa mudou desde a prévia; prepare uma nova.");
        }

        if (command.Action != Domain.Mcp.Enums.McpPreviewAction.Delete)
            return McpRejected("PREVIEW_PAYLOAD_INVALID", "A ação MCP de despesa é inválida.");

        var deleted = await _repository.TryDeleteMcpAsync(
            command.TargetId,
            _usuarioLogado.IdContextoDados,
            expected,
            cancellationToken);
        if (deleted)
            return McpCompleted(command.TargetId, operationId, resultHash);

        var observed = await _repository.GetById(command.TargetId);
        return observed is null
            ? new McpApplicationMutationResult(
                McpApplicationMutationState.Unknown,
                command.TargetId,
                null,
                null,
                "EFFECT_OUTCOME_UNKNOWN",
                "A despesa está ausente, mas a causalidade da exclusão não pôde ser comprovada.")
            : McpConflict("A despesa mudou desde a prévia; prepare uma nova.");
    }

    public async Task<McpApplicationMutationResult> SincronizarAgrupamentoMcpAsync(
        string groupingExpenseId,
        decimal expectedParentAmount,
        decimal baseAmount,
        string operationId,
        string resultHash,
        CancellationToken cancellationToken = default)
    {
        var updated = await _repository.TrySynchronizeGroupingMcpAsync(
            groupingExpenseId,
            _usuarioLogado.IdContextoDados,
            expectedParentAmount,
            baseAmount,
            operationId,
            resultHash,
            cancellationToken);
        if (updated is not null)
            return McpCompleted(updated.Id, operationId, resultHash);
        var current = await _repository.GetById(groupingExpenseId);
        return current is null || current.UsuarioId != _usuarioLogado.IdContextoDados
            ? McpRejected("GROUPING_RELATIONSHIP_INVALID", "A despesa agrupadora não foi encontrada para esta conta.")
            : McpConflict("A despesa agrupadora mudou desde a prévia; prepare uma nova.");
    }

    private static bool TryMcpSnapshot(
        IReadOnlyDictionary<string, object?> values,
        out McpExpenseSnapshot snapshot)
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
        snapshot = new McpExpenseSnapshot(
            year,
            month,
            description,
            amount,
            categoryId,
            McpText(values, "groupingExpenseId"),
            McpText(values, "expenseOriginId"),
            McpBool(values, "isInstallment"),
            McpBool(values, "isRecurring"),
            McpNullableInt(values, "installmentNumber"),
            McpNullableInt(values, "installmentCount"));
        return true;
    }

    private static string? McpText(IReadOnlyDictionary<string, object?> values, string key) =>
        values.GetValueOrDefault(key)?.ToString();

    private static bool McpBool(IReadOnlyDictionary<string, object?> values, string key) =>
        bool.TryParse(values.GetValueOrDefault(key)?.ToString(), out var value) && value;

    private static int? McpNullableInt(IReadOnlyDictionary<string, object?> values, string key) =>
        int.TryParse(values.GetValueOrDefault(key)?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static McpApplicationMutationResult McpCompleted(string id, string operationId, string resultHash) =>
        new(McpApplicationMutationState.Completed, id, operationId, resultHash, null, null);

    private static McpApplicationMutationResult McpConflict(string message) =>
        new(McpApplicationMutationState.ConflictChanged, null, null, null, "CONFLICT_CHANGED", message);

    private static McpApplicationMutationResult McpRejected(string code, string message) =>
        new(McpApplicationMutationState.Rejected, null, null, null, code, message);

    private static ResultDespesaDTO ObterDespesaDTO(Despesa despesa, AcumuladoMensalReport? reportAcumulado = null)
    {
        var result = new ResultDespesaDTO()
        {
            Ano = despesa.Ano,
            Mes = despesa.Mes,
            CategoriaNome = despesa.Categoria?.Nome,
            CategoriaId = despesa.CategoriaId,
            Id = despesa.Id,
            Descricao = despesa.Descricao,
            Valor = despesa.Valor,
            ReportAcumulado = reportAcumulado,
            EhDespesaAgrupadora = despesa.DespesaAgrupadora,
            IdDespesaAgrupadora = despesa.IdDespesaAgrupadora,
            Agrupadora = despesa.Agrupadora is not null ? ObterDespesaDTO(despesa.Agrupadora) : null,
            DespesaOrigemId = despesa.DespesaOrigemId,
            IsRecorrente = despesa.IsRecorrente,
            IsParcelado = despesa.IsParcelado,
            ParcelaAtual = despesa.ParcelaAtual,
            TotalParcelas = despesa.TotalParcelas
        };

        return result;
    }

    public async Task<Result> LancarDespesaEmLoteAsync(LancarDespesaLoteDTO dto)
    {
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        if (dto.QuantidadeMeses > 24)
            return Result.Failure(Error.Validation("A quantidade de meses para recorrência ou parcelamento não pode ser superior a 24 meses."));

        Categoria categoria = await _categoriaRepository.GetById(dto.CategoriaId);

        if (categoria == null)
            return Result.Failure(Error.NotFound("Categoria informada não existe!"));

        var despesas = new List<Domain.Entity.Despesa>();
        var contextosAgrupadoras = new Dictionary<string, DespesaAgrupamentoContexto>();
        string despesaOrigemId = Guid.NewGuid().ToString();

        // Se tem agrupadora, buscar a agrupadora do mês inicial como referência
        Despesa agrupadoaReferencia = null;
        if (!string.IsNullOrEmpty(dto.IdDespesaAgrupadora))
        {
            agrupadoaReferencia = await _repository.GetById(dto.IdDespesaAgrupadora);
            if (agrupadoaReferencia == null)
                return Result.Failure(Error.NotFound("Despesa agrupadora não encontrada."));
        }

        int anoCorrente = dto.AnoInicial;
        int mesCorrente = dto.MesInicial;

        decimal valorParcelaNormal = dto.IsParcelado ? Math.Round(dto.ValorTotal / dto.QuantidadeMeses, 2) : dto.ValorTotal;
        decimal valorAcumulado = 0;

        for (int i = 1; i <= dto.QuantidadeMeses; i++)
        {
            decimal valor = valorParcelaNormal;

            if (dto.IsParcelado)
            {
                if (i == dto.QuantidadeMeses)
                {
                    valor = dto.ValorTotal - valorAcumulado;
                }
                valorAcumulado += valor;
            }

            string descricao = dto.Descricao;

            var despesa = new Despesa(anoCorrente, mesCorrente, descricao, valor, categoria, _usuarioLogado.UsuarioContextoDados)
            {
                DespesaOrigemId = despesaOrigemId,
                IsParcelado = dto.IsParcelado,
                IsRecorrente = dto.IsRecorrenteFixa,
                ParcelaAtual = dto.IsParcelado ? i : null,
                TotalParcelas = dto.IsParcelado ? dto.QuantidadeMeses : null
            };

            // NOVO: Vincular à agrupadora do mês correspondente
            if (agrupadoaReferencia != null)
            {
                var agrupadoaDoMes = await ObterOuClonarAgrupadora(agrupadoaReferencia, anoCorrente, mesCorrente);

                await CapturarAgrupadoraAfetada(contextosAgrupadoras, agrupadoaDoMes.Id);
                _agrupamentoService.Vincular(despesa, agrupadoaDoMes);
            }

            despesas.Add(despesa);

            mesCorrente++;
            if (mesCorrente > 12)
            {
                mesCorrente = 1;
                anoCorrente++;
            }
        }

        if (despesas.Any())
            await _repository.InsertManyAsync(despesas);

        await SincronizarAgrupadoras(contextosAgrupadoras.Values);

        return Result.Success();
    }

    public async Task<Result> AtualizarDespesaEmLoteAsync(string id, AtualizarLoteDespesaDTO dto)
    {
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var despesaAlvo = await _repository.GetById(id);
        if (despesaAlvo == null)
            return Result.Failure(Error.NotFound("Despesa alvo não encontrada."));

        if (string.IsNullOrEmpty(despesaAlvo.DespesaOrigemId))
            return Result.Failure(Error.Validation("Esta despesa não faz parte de um lote."));

        Categoria categoria = await _categoriaRepository.GetById(dto.NovaCategoriaId);
        if (categoria == null)
            return Result.Failure(Error.NotFound("Categoria não encontrada."));

        IEnumerable<Domain.Entity.Despesa> loteCompleto = await _repository.GetDespesasDoLoteAsync(despesaAlvo.DespesaOrigemId);
        IEnumerable<Domain.Entity.Despesa> despesasParaAtualizar = loteCompleto;

        if (dto.Modificador == ModificadorLote.ApenasEsta)
        {
            despesasParaAtualizar = loteCompleto.Where(d => d.Id == despesaAlvo.Id);
        }
        else if (dto.Modificador == ModificadorLote.EstaEProximas)
        {
            if (despesaAlvo.IsParcelado)
                despesasParaAtualizar = loteCompleto.Where(d => d.ParcelaAtual >= despesaAlvo.ParcelaAtual);
            else
                despesasParaAtualizar = loteCompleto.Where(d => d.Ano > despesaAlvo.Ano || (d.Ano == despesaAlvo.Ano && d.Mes >= despesaAlvo.Mes));
        }

        despesasParaAtualizar = despesasParaAtualizar.ToList();

        var contextosAgrupadoras = new Dictionary<string, DespesaAgrupamentoContexto>();
        Despesa agrupadoraReferencia = null;
        var descricaoNormalizada = NormalizarDescricaoParcela(dto.NovaDescricao);

        if (dto.IdDespesaAgrupadora != null && dto.IdDespesaAgrupadora.PossuiValor())
        {
            agrupadoraReferencia = await _repository.GetById(dto.IdDespesaAgrupadora);

            if (agrupadoraReferencia == null)
                return Result.Failure(Error.NotFound("Despesa agrupadora não encontrada."));
        }

        foreach (var despesa in despesasParaAtualizar)
        {
            await CapturarAgrupadoraAfetada(contextosAgrupadoras, despesa.IdDespesaAgrupadora);

            var resultAgrupamento = await AtualizarAgrupamentoDespesaEmLote(
                despesa,
                dto.IdDespesaAgrupadora,
                agrupadoraReferencia,
                contextosAgrupadoras);

            if (resultAgrupamento.IsFailure)
                return Result.Failure(resultAgrupamento.Error);

            despesa.Descricao = descricaoNormalizada;
            despesa.AtualizarValor(dto.NovoValor);

            despesa.PreencherCategoria(categoria);
        }

        await _repository.UpdateManyAsync(despesasParaAtualizar);

        await SincronizarAgrupadoras(contextosAgrupadoras.Values);

        return Result.Success();
    }

    private async Task<Result> AtualizarAgrupamentoDespesaEmLote(
        Despesa despesa,
        string idDespesaAgrupadora,
        Despesa agrupadoraReferencia,
        Dictionary<string, DespesaAgrupamentoContexto> contextosAgrupadoras)
    {
        if (idDespesaAgrupadora == null)
            return Result.Success();

        if (idDespesaAgrupadora.PossuiValor() == false)
        {
            _agrupamentoService.RemoverVinculo(despesa);
            return Result.Success();
        }

        if (agrupadoraReferencia == null)
            return Result.Failure(Error.NotFound("Despesa agrupadora não encontrada."));

        var agrupadoraDoMes = await ObterOuClonarAgrupadora(agrupadoraReferencia, despesa.Ano, despesa.Mes);
        await CapturarAgrupadoraAfetada(contextosAgrupadoras, agrupadoraDoMes.Id);

        if (despesa.IdDespesaAgrupadora == agrupadoraDoMes.Id)
        {
            despesa.Agrupadora = agrupadoraDoMes;
            return Result.Success();
        }

        _agrupamentoService.RemoverVinculo(despesa);

        _agrupamentoService.Vincular(despesa, agrupadoraDoMes);

        return Result.Success();
    }

    private static string NormalizarDescricaoParcela(string descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao))
            return descricao;

        return Regex.Replace(descricao, @"(?:\s+\(\d+/\d+\))+$", string.Empty).Trim();
    }

    public async Task<Result> ExcluirDespesaEmLoteAsync(string id, ModificadorLote modificador)
    {
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var despesaAlvo = await _repository.GetById(id);
        if (despesaAlvo == null)
            return Result.Failure(Error.NotFound("Despesa alvo não encontrada."));

        if (string.IsNullOrEmpty(despesaAlvo.DespesaOrigemId))
            return Result.Failure(Error.Validation("Esta despesa não faz parte de um lote."));

        IEnumerable<Domain.Entity.Despesa> loteCompleto = await _repository.GetDespesasDoLoteAsync(despesaAlvo.DespesaOrigemId);
        IEnumerable<Domain.Entity.Despesa> despesasParaExcluir = loteCompleto;

        if (modificador == ModificadorLote.ApenasEsta)
        {
            despesasParaExcluir = loteCompleto.Where(d => d.Id == despesaAlvo.Id);
        }
        else if (modificador == ModificadorLote.EstaEProximas)
        {
            if (despesaAlvo.IsParcelado)
                despesasParaExcluir = loteCompleto.Where(d => d.ParcelaAtual >= despesaAlvo.ParcelaAtual);
            else
                despesasParaExcluir = loteCompleto.Where(d => d.Ano > despesaAlvo.Ano || (d.Ano == despesaAlvo.Ano && d.Mes >= despesaAlvo.Mes));
        }

        var contextosAgrupadoras = new Dictionary<string, DespesaAgrupamentoContexto>();
        foreach (var despesa in despesasParaExcluir)
        {
            if (despesa.EstaAgrupada())
                await CapturarAgrupadoraAfetada(contextosAgrupadoras, despesa.IdDespesaAgrupadora);
        }

        await _repository.DeleteManyAsync(despesasParaExcluir);

        await SincronizarAgrupadoras(contextosAgrupadoras.Values);

        return Result.Success();
    }

    private async Task CapturarAgrupadoraAfetada(
        Dictionary<string, DespesaAgrupamentoContexto> contextosAgrupadoras,
        string idDespesaAgrupadora)
    {
        if (idDespesaAgrupadora.PossuiValor() == false || contextosAgrupadoras.ContainsKey(idDespesaAgrupadora))
            return;

        var contexto = await _agrupamentoService.CapturarContextoAsync(idDespesaAgrupadora);
        if (contexto != null)
            contextosAgrupadoras[idDespesaAgrupadora] = contexto;
    }

    private async Task SincronizarAgrupadoras(IEnumerable<DespesaAgrupamentoContexto> contextosAgrupadoras)
    {
        foreach (var contexto in contextosAgrupadoras)
        {
            await _agrupamentoService.SincronizarAgrupadoraAsync(contexto);
        }
    }

    private async Task<Despesa> ObterOuClonarAgrupadora(Despesa agrupadoaReferencia, int ano, int mes)
    {
        // Caso 1: A agrupadora é do próprio mês que estamos criando
        if (agrupadoaReferencia.Ano == ano && agrupadoaReferencia.Mes == mes)
            return agrupadoaReferencia;

        // Caso 2: Buscar agrupadora existente no mês/ano alvo
        var agrupadorasNoMes = await _repository.GetWhere(
            x => x.Ano == ano
                 && x.Mes == mes
                 && x.Descricao == agrupadoaReferencia.Descricao
                 && x.CategoriaId == agrupadoaReferencia.CategoriaId
                 && x.UsuarioId == agrupadoaReferencia.UsuarioId);

        var agrupadoaExistente = agrupadorasNoMes.FirstOrDefault(x => x.EhAgrupadora()
            || x.Descricao == agrupadoaReferencia.Descricao);

        if (agrupadoaExistente != null)
            return agrupadoaExistente;

        // Caso 3: Clonar a agrupadora para o mês futuro
        var clone = agrupadoaReferencia.Clone();
        clone.Ano = ano;
        clone.Mes = mes;
        clone.AtualizarValor(0); // Inicia com valor zero, será incrementado pelas filhas
        clone.MarcarDespesaComoAgrupadora();

        // Limpar dados de lote da agrupadora clonada (agrupadora não faz parte de lote)
        clone.DespesaOrigemId = null;
        clone.IsParcelado = false;
        clone.IsRecorrente = false;
        clone.ParcelaAtual = null;
        clone.TotalParcelas = null;

        await _repository.Add(clone);
        return clone;
    }
}

#nullable restore annotations
