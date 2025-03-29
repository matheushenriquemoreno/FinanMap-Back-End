using Application.DTOs;
using Application.Interface;

using Application.Shared.Transacao.DTOs;
using Domain.Entity;
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
        Categoria? categoria = await _categoriaRepository.GetByID(createDTO.CategoriaId);

        if (categoria == null)
            return Result.Failure<ResultRendimentoDTO>(Error.NotFound("Categoria informada não existe!"));

        Rendimento rendimento = new Rendimento(createDTO.Ano, createDTO.Mes, createDTO.Descricao, createDTO.Valor, categoria, _usuarioLogado.Usuario);

        await _rendimentoRepository.Add(rendimento);

        var reportAcumulado = await _acumuladoMensalReportRepository.Obter(rendimento.Mes, rendimento.Ano, _usuarioLogado.Id);

        ResultRendimentoDTO rendimentoDTO = ObterRendimentoDTO(rendimento, reportAcumulado);

        return Result.Success(rendimentoDTO);
    }

    public async Task<Result<ResultRendimentoDTO>> Atualizar(UpdateRendimentoDTO updateDTO)
    {
        Rendimento rendimento = await _rendimentoRepository.GetByID(updateDTO.Id);

        if (rendimento == null)
            return Result.Failure<ResultRendimentoDTO>(Error.NotFound("Rendimento informado não existe!"));

        Categoria categoria = await _categoriaRepository.GetByID(updateDTO.CategoriaId);

        if (categoria == null)
            return Result.Failure<ResultRendimentoDTO>(Error.NotFound("Categoria informada não existe!"));

        rendimento.Atualizar(updateDTO.Descricao, updateDTO.Valor, categoria);

        await _rendimentoRepository.Update(rendimento);

        var reportAcumulado = await _acumuladoMensalReportRepository.Obter(rendimento.Mes, rendimento.Ano, _usuarioLogado.Id);

        ResultRendimentoDTO rendimentoDTO = ObterRendimentoDTO(rendimento, reportAcumulado);

        return Result.Success(rendimentoDTO);
    }

    public async Task<Result> Excluir(string id)
    {
        var rendimento = await _rendimentoRepository.GetByID(id);

        if (rendimento == null)
            return Result.Failure(Error.NotFound("Rendimento informado não existe!"));

        await _rendimentoRepository.Delete(rendimento);

        return Result.Success();
    }

    public async Task<Result<ResultRendimentoDTO>> ObterPeloID(string id)
    {
        var rendimento = await _rendimentoRepository.GetByID(id);

        if (rendimento == null)
            return Result.Failure<ResultRendimentoDTO>(Error.NotFound("Rendimento informado não existe!"));

        return Result.Success(rendimento.Adapt<ResultRendimentoDTO>());
    }

    public async Task<List<ResultRendimentoDTO>> ObterRendimentoMes(int mes, int ano)
    {
        var rendimentos = await _rendimentoRepository.ObterPeloMes(mes, ano, _usuarioLogado.Id);

        return rendimentos.Select(x => ObterRendimentoDTO(x)).ToList();
    }

    public async Task<Result<ResultRendimentoDTO>> AtualizarValor(UpdateValorTransacaoDTO updateValorTransacaoDTO)
    {
        Rendimento rendimento = await _rendimentoRepository.GetByID(updateValorTransacaoDTO.Id);

        if (rendimento == null)
            return Result.Failure<ResultRendimentoDTO>(Error.NotFound("Rendimento informado não existe!"));

        rendimento.Atualizar(updateValorTransacaoDTO.Valor);

        await _rendimentoRepository.Update(rendimento);

        var reportAcumulado = await _acumuladoMensalReportRepository.Obter(rendimento.Mes, rendimento.Ano, _usuarioLogado.Id);

        return Result.Success(ObterRendimentoDTO(rendimento, reportAcumulado));
    }


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
    #endregion
}
