using Application.DTOs;
using Application.Interface;
using Application.Shared.Transacao.DTOs;
using Domain.Entity;
using Domain.Login.Interfaces;
using Domain.Relatorios.AcumuladoMensal;
using Domain.Relatorios.Entity;
using Domain.Repository;

namespace Application.Service;

public class InvestimentoService : IInvestimentoService
{
    private readonly IInvestimentoRepository _investimentoRepository;
    private readonly ICategoriaRepository _categoriaRepository;
    private readonly IAcumuladoMensalReportRepository _acumuladoMensalReportRepository;
    private readonly IUsuarioLogado _usuarioLogado;

    public InvestimentoService(IAcumuladoMensalReportRepository acumuladoMensalReportRepository, IInvestimentoRepository investimentoRepository, ICategoriaRepository categoriaRepository, IUsuarioLogado usuarioLogado)
    {
        _acumuladoMensalReportRepository = acumuladoMensalReportRepository;
        _investimentoRepository = investimentoRepository;
        _categoriaRepository = categoriaRepository;
        _usuarioLogado = usuarioLogado;
    }

    public async Task<Result<ResultInvestimentoDTO>> Adicionar(CreateInvestimentoDTO createDTO)
    {
        Categoria categoria = await _categoriaRepository.GetByID(createDTO.CategoriaId);

        if (categoria == null)
            return Result.Failure<ResultInvestimentoDTO>(Error.NotFound("Categoria informada não existe!"));

        Investimento investimento = new(createDTO.Ano, createDTO.Mes, createDTO.Descricao, createDTO.Valor, categoria, _usuarioLogado.Usuario);

        await _investimentoRepository.Add(investimento);

        var reportAcumulado = await _acumuladoMensalReportRepository.Obter(investimento.Mes, investimento.Ano, _usuarioLogado.Id);

        ResultInvestimentoDTO rendimentoDTO = ObterResultInvestimentoDTO(investimento, reportAcumulado);

        return Result.Success(rendimentoDTO);
    }

    public async Task<Result<ResultInvestimentoDTO>> Atualizar(UpdateInvestimentoDTO updateDTO)
    {
        Investimento investimento = await _investimentoRepository.GetByID(updateDTO.Id);

        if (investimento == null)
            return Result.Failure<ResultInvestimentoDTO>(Error.NotFound("Rendimento informado não existe!"));

        Categoria categoria = await _categoriaRepository.GetByID(updateDTO.CategoriaId);

        if (categoria == null)
            return Result.Failure<ResultInvestimentoDTO>(Error.NotFound("Categoria informada não existe!"));

        investimento.Atualizar(updateDTO.Descricao, updateDTO.Valor, categoria);

        await _investimentoRepository.Update(investimento);

        var reportAcumulado = await _acumuladoMensalReportRepository.Obter(investimento.Mes, investimento.Ano, _usuarioLogado.Id);

        return Result.Success(ObterResultInvestimentoDTO(investimento, reportAcumulado));
    }

    public async Task<Result> Excluir(string id)
    {
        var investimento = await _investimentoRepository.GetByID(id);

        if (investimento == null)
            return Result.Failure(Error.NotFound("Investimento informado não existente"));

        await _investimentoRepository.Delete(investimento);

        return Result.Success();
    }

    public async Task<Result<ResultInvestimentoDTO>> ObterPeloID(string id)
    {
        var investimento = await _investimentoRepository.GetByID(id);

        if (investimento == null)
            return Result.Failure<ResultInvestimentoDTO>(Error.NotFound("Investimento informado não existente"));

        return Result.Success(ObterResultInvestimentoDTO(investimento));
    }

    public async Task<List<ResultInvestimentoDTO>> ObterMesAno(int mes, int ano)
    {
        var despesas = await _investimentoRepository.ObterPeloMes(mes, ano, _usuarioLogado.Id);

        return despesas.Select(x => ObterResultInvestimentoDTO(x)).ToList();
    }

    public async Task<Result<ResultInvestimentoDTO>> AtualizarValor(UpdateValorTransacaoDTO updateValorTransacaoDTO)
    {
        var investimento = await _investimentoRepository.GetByID(updateValorTransacaoDTO.Id);

        if (investimento == null)
            return Result.Failure<ResultInvestimentoDTO>(Error.NotFound("Investimento informado não existe!"));

        investimento.Atualizar(updateValorTransacaoDTO.Valor);

        await _investimentoRepository.Update(investimento);

        var reportAcumulado = await _acumuladoMensalReportRepository.Obter(investimento.Mes, investimento.Ano, _usuarioLogado.Id);

        return Result.Success(ObterResultInvestimentoDTO(investimento, reportAcumulado));
    }

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
}
