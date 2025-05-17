using Application.DTOs;
using Application.Interfaces;
using Application.Shared.Transacao.DTOs;
using Domain.Entity;
using Domain.Login.Interfaces;
using Domain.Relatorios.AcumuladoMensal;
using Domain.Relatorios.Entity;
using Domain.Repository;

namespace Application.Services;

public class DespesaService : IDespesaService
{
    private readonly IDespesaRepository _repository;
    private readonly ICategoriaRepository _categoriaRepository;
    private readonly IAcumuladoMensalReportRepository _acumuladoMensalReportRepository;
    private readonly IUsuarioLogado _usuarioLogado;

    public DespesaService(IDespesaRepository repository, ICategoriaRepository categoriaRepository, IAcumuladoMensalReportRepository acumuladoMensalReportRepository, IUsuarioLogado usuarioLogado)
    {
        _repository = repository;
        _categoriaRepository = categoriaRepository;
        _acumuladoMensalReportRepository = acumuladoMensalReportRepository;
        _usuarioLogado = usuarioLogado;
    }

    public async Task<Result<ResultDespesaDTO>> Adicionar(CreateDespesaDTO createDTO)
    {
        Categoria? categoria = await _categoriaRepository.GetByID(createDTO.CategoriaId);

        if (categoria == null)
            return Result.Failure<ResultDespesaDTO>(Error.NotFound("Categoria informada não existe!"));

        Despesa despesa = new Despesa(createDTO.Ano, createDTO.Mes, createDTO.Descricao, createDTO.Valor, categoria, _usuarioLogado.Usuario);

        if (!string.IsNullOrEmpty(createDTO.IdDespesaAgrupadora))
        {
            var result = await VincularDespesaAUmaAgrupadora(createDTO.IdDespesaAgrupadora, despesa);

            if (result.IsFailure)
                return Result.Failure<ResultDespesaDTO>(result.Error);
        }

        await _repository.Add(despesa);

        var reportAcumulado = await _acumuladoMensalReportRepository.Obter(despesa.Mes, despesa.Ano, _usuarioLogado.Id);

        ResultDespesaDTO rendimentoDTO = ObterDespesaDTO(despesa, reportAcumulado);

        return Result.Success(rendimentoDTO);
    }

    public async Task<Result<ResultDespesaDTO>> Atualizar(UpdateDespesaDTO updateDTO)
    {
        var despesa = await _repository.GetByID(updateDTO.Id);

        if (despesa == null)
            return Result.Failure<ResultDespesaDTO>(Error.NotFound("Despesa informada não existente"));

        Categoria categoria = await _categoriaRepository.GetByID(updateDTO.CategoriaId);

        if (categoria == null)
            return Result.Failure<ResultDespesaDTO>(Error.NotFound("Categoria informada não existe!"));

        if (despesa.EhAgrupadora())
        {
            if (updateDTO.IdDespesaAgrupadora.PossuiValor())
                return Result.Failure<ResultDespesaDTO>(Error.NotFound("Despesa que e usada para agrupar outras, não pode ser vinculada a nenhuma outra."));

            var valorAgrupamento = await _repository.ObterValorTotalDespesasDaAgrupadora(despesa.Id);

            if (valorAgrupamento > updateDTO.Valor)
                return Result.Failure<ResultDespesaDTO>(Error.Validation($"Valor informado menor que o total das despesas agrupadas, valor total: {valorAgrupamento}"));
        }

        despesa.Descricao = updateDTO.Descricao;
        despesa.AtualizarValor(updateDTO.Valor);
        despesa.PreencherCategoria(categoria);

        await AtualizarAgrupamento(despesa, updateDTO);

        await _repository.Update(despesa);

        await AtualizarDespesaAgrupadoraCasoNecessario(despesa);

        var reportAcumulado = await _acumuladoMensalReportRepository.Obter(despesa.Mes, despesa.Ano, _usuarioLogado.Id);

        ResultDespesaDTO rendimentoDTO = ObterDespesaDTO(despesa, reportAcumulado);

        return Result.Success(rendimentoDTO);
    }

    private async Task AtualizarDespesaAgrupadoraCasoNecessario(Despesa despesa)
    {
        if (despesa.EstaAgrupada())
        {
            var despesaAgrupadora = await _repository.GetByID(despesa.IdDespesaAgrupadora);

            var valorAgrupamento = await _repository.ObterValorTotalDespesasDaAgrupadora(despesa.IdDespesaAgrupadora);

            if (despesaAgrupadora.Valor < valorAgrupamento)
            {
                despesaAgrupadora.Valor = valorAgrupamento;

                await _repository.Update(despesaAgrupadora);
            }
        }
    }

    private async Task<Result> AtualizarAgrupamento(Despesa despesa, UpdateDespesaDTO updateDTO)
    {
        // Cenario de remover vinculo com a despesa agrupadora
        if (despesa.EstaAgrupada() &&
            updateDTO.IdDespesaAgrupadora.PossuiValor() == false)
        {
            await DesvincularDespesaDaAgrupadora(despesa, despesa.IdDespesaAgrupadora);
            return Result.Success();
        }

        // cenario de agrupamento após a criação da despesa
        if (updateDTO.IdDespesaAgrupadora.PossuiValor() &&
                despesa.EstaAgrupada() == false)
        {
            var result = await VincularDespesaAUmaAgrupadora(updateDTO.IdDespesaAgrupadora, despesa);

            if (result.IsFailure)
                return Result.Failure(result.Error);

            return Result.Success();
        }

        // cenario de troca de agrupamento, era de uma despesa foi para outra.
        if (updateDTO.IdDespesaAgrupadora.PossuiValor() &&
                despesa.IdDespesaAgrupadora != updateDTO.IdDespesaAgrupadora)
        {
            await DesvincularDespesaDaAgrupadora(despesa, despesa.IdDespesaAgrupadora);

            var result = await VincularDespesaAUmaAgrupadora(updateDTO.IdDespesaAgrupadora, despesa);

            if (result.IsFailure)
                return Result.Failure(result.Error);

            return result;
        }

        return Result.Success();
    }

    public async Task<Result> Excluir(string id)
    {
        Despesa despesa = await _repository.GetByID(id);

        if (despesa == null)
            return Result.Failure(Error.NotFound("Despesa informada não existente"));

        if (despesa.EstaAgrupada())
        {
            var despesaAgrupadora = await _repository.GetByID(despesa.IdDespesaAgrupadora);
            await RemoverVinculoAgrupadoraEhAtualizar(despesaAgrupadora, despesa);
        }

        await _repository.Delete(despesa);

        return Result.Success();
    }

    public async Task<Result<ResultDespesaDTO>> ObterPeloID(string id)
    {
        var despesa = await _repository.GetByID(id);

        if (despesa == null)
            return Result.Failure<ResultDespesaDTO>(Error.NotFound("Despesa informada não existente"));

        return Result.Success(ObterDespesaDTO(despesa));
    }

    public async Task<List<ResultDespesaDTO>> ObterMesAno(int mes, int ano, string descricao)
    {
        var despesas = await _repository.ObterPeloMes(mes, ano, _usuarioLogado.Id, descricao);

        return despesas.Select(x => ObterDespesaDTO(x)).ToList();
    }

    public async Task<List<ResultDespesaDTO>> ObterDespesasDaAgrupadora(string idDespesa)
    {
        var despesas = await _repository.ObterDespesasDaAgrupadora(idDespesa);

        return despesas.Select(x => ObterDespesaDTO(x)).ToList();
    }

    public async Task<Result<ResultDespesaDTO>> AtualizarValor(UpdateValorTransacaoDTO updateValorTransacaoDTO)
    {
        var despesa = await _repository.GetByID(updateValorTransacaoDTO.Id);

        if (despesa == null)
            return Result.Failure<ResultDespesaDTO>(Error.NotFound("Despesa informado não existe!"));
        
        if (despesa.EhAgrupadora())
        {
            var valorAgrupamento = await _repository.ObterValorTotalDespesasDaAgrupadora(despesa.Id);

            if (valorAgrupamento > updateValorTransacaoDTO.Valor)
                return Result.Failure<ResultDespesaDTO>(Error.Validation($"Valor informado menor que o total das despesas agrupadas, valor total: {valorAgrupamento}"));
        }

        despesa.AtualizarValor(updateValorTransacaoDTO.Valor);

        await _repository.Update(despesa);

        await AtualizarDespesaAgrupadoraCasoNecessario(despesa);

        var reportAcumulado = await _acumuladoMensalReportRepository.Obter(despesa.Mes, despesa.Ano, _usuarioLogado.Id);

        return Result.Success(ObterDespesaDTO(despesa, reportAcumulado));
    }

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
        };

        return result;
    }

    private async Task<Result> VincularDespesaAUmaAgrupadora(string idDespesaAgrupadora, Despesa despesa)
    {
        var despesaAgrupadora = await _repository.GetByID(idDespesaAgrupadora);

        if (despesaAgrupadora == null)
            return Result.Failure(Error.NotFound("Despesa para realizar agrupamento não existe!"));

        despesa.AdicionarDespesaAgrupadora(despesaAgrupadora);
        despesaAgrupadora.MarcarDespesaComoAgrupadora();

        var valorAgrupamento = await _repository.ObterValorTotalDespesasDaAgrupadora(despesaAgrupadora.Id);

        valorAgrupamento += despesa.Valor;

        if (valorAgrupamento > despesaAgrupadora.Valor)
        {
            var diferenca = valorAgrupamento - despesaAgrupadora.Valor;

            despesaAgrupadora.AtualizarValor(despesaAgrupadora.Valor + diferenca);
        }

        await _repository.Update(despesaAgrupadora);

        return Result.Success();
    }

    private async Task DesvincularDespesaDaAgrupadora(Despesa despesa, string idDespesaAgrupadora)
    {
        despesa.RemoverDespesaAgrupadora();

        var despesaAgrupadora = await _repository.GetByID(idDespesaAgrupadora);
        await RemoverVinculoAgrupadoraEhAtualizar(despesaAgrupadora, despesa);
    }

    private async Task RemoverVinculoAgrupadoraEhAtualizar(Despesa despesaAgrupadora, Despesa despesa)
    {
        despesaAgrupadora.DiminuirAgrupamento(despesa);
        await _repository.Update(despesaAgrupadora);
    }
}
