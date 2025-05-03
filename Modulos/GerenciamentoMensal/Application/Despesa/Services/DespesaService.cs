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
                updateDTO.Valor = valorAgrupamento;
        }

        despesa.Descricao = updateDTO.Descricao;
        despesa.Valor = updateDTO.Valor;
        despesa.PreencherCategoria(categoria);

        if (despesa.EstaAgrupada() &&
            updateDTO.IdDespesaAgrupadora.PossuiValor() == false)
        {
            await DesvincularDespesaDaAgrupadora(despesa.IdDespesaAgrupadora, despesa);
        }
        else if (updateDTO.IdDespesaAgrupadora.PossuiValor() &&
                despesa.EstaAgrupada() == false)
        {
            var result = await VincularDespesaAUmaAgrupadora(updateDTO.IdDespesaAgrupadora, despesa);

            if (result.IsFailure)
                return Result.Failure<ResultDespesaDTO>(result.Error);
        }
        else if (updateDTO.IdDespesaAgrupadora.PossuiValor() && 
                despesa.IdDespesaAgrupadora != updateDTO.IdDespesaAgrupadora)
        {
            await DesvincularDespesaDaAgrupadora(despesa.IdDespesaAgrupadora, despesa);

            var result = await VincularDespesaAUmaAgrupadora(updateDTO.IdDespesaAgrupadora, despesa);

            if (result.IsFailure)
                return Result.Failure<ResultDespesaDTO>(result.Error);
        }

        await _repository.Update(despesa);

        var reportAcumulado = await _acumuladoMensalReportRepository.Obter(despesa.Mes, despesa.Ano, _usuarioLogado.Id);

        ResultDespesaDTO rendimentoDTO = ObterDespesaDTO(despesa, reportAcumulado);

        return Result.Success(rendimentoDTO);
    }

    public async Task<Result> Excluir(string id)
    {
        Despesa despesa = await _repository.GetByID(id);

        if (despesa == null)
            return Result.Failure(Error.NotFound("Despesa informada não existente"));

        if (despesa.EstaAgrupada())
        {
            var despesaAgrupadora = await _repository.GetByID(despesa.IdDespesaAgrupadora);
            await AtualizarAgrupadoraAoRemoverVinculo(despesaAgrupadora);
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

        despesa.AtualizarValor(updateValorTransacaoDTO.Valor);

        await _repository.Update(despesa);

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
            EhDespesaAgrupadora = despesa.DespesaAgrupadora
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

        await AtualizarValorAgrupadora(despesa, despesaAgrupadora);

        await _repository.Update(despesaAgrupadora);

        return Result.Success();
    }


    private async Task DesvincularDespesaDaAgrupadora(string idDespesaAgrupadora, Despesa despesa)
    {
        var despesaAgrupadora = await _repository.GetByID(idDespesaAgrupadora);

        despesa.RemoverDespesaAgrupadora();

        await AtualizarAgrupadoraAoRemoverVinculo(despesaAgrupadora);
    }

    private async Task AtualizarValorAgrupadora(Despesa despesa, Despesa despesaAgrupadora)
    {
        var valorAgrupamento = await _repository.ObterValorTotalDespesasDaAgrupadora(despesaAgrupadora.Id);
        valorAgrupamento += despesa.Valor;

        if (valorAgrupamento > despesaAgrupadora.Valor)
        {
            var diferenca = valorAgrupamento - despesaAgrupadora.Valor;

            despesaAgrupadora.AtualizarValor(despesaAgrupadora.Valor + diferenca);
        }
    }

    private async Task AtualizarAgrupadoraAoRemoverVinculo(Despesa despesaAgrupadora)
    {
        if (despesaAgrupadora.QuantidadeRegistros == 1)
        {
            despesaAgrupadora.DesmarcarDespesaComoAgrupadora();
        }
        else
        {
            despesaAgrupadora.DiminuirRegistro();
        }

        await _repository.Update(despesaAgrupadora);
    }

}
