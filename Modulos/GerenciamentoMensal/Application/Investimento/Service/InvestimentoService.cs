using Application.DTOs;
using Application.Interface;
using Application.MetaFinanceira.DTOs;
using Application.MetaFinanceira.Interface;
using Application.Shared.Transacao.DTOs;
using Domain.Compartilhamento.Entity;
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

        Categoria categoria = await _categoriaRepository.GetById(updateDTO.CategoriaId);

        if (categoria == null)
            return Result.Failure<ResultInvestimentoDTO>(Error.NotFound("Categoria informada não existe!"));

        investimento.Atualizar(updateDTO.Descricao, updateDTO.Valor, categoria);

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

        investimento.AtualizarValor(updateValorTransacaoDTO.Valor);

        await _investimentoRepository.Update(investimento);

        var reportAcumulado = await _acumuladoMensalReportRepository.Obter(investimento.Mes, investimento.Ano, _usuarioLogado.IdContextoDados);

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
