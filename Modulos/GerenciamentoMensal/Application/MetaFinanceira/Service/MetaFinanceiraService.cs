using Application.MetaFinanceira.DTOs;
using Application.MetaFinanceira.Interface;
using Domain.Compartilhamento.Entity;
using Domain.Entity;
using Domain.Login.Interfaces;
using Domain.MetaFinanceira.Repository;

namespace Application.MetaFinanceira.Service;

public class MetaFinanceiraService : IMetaFinanceiraService
{
    private readonly IMetaFinanceiraRepository _repository;
    private readonly IUsuarioLogado _usuarioLogado;

    public MetaFinanceiraService(
        IMetaFinanceiraRepository repository,
        IUsuarioLogado usuarioLogado)
    {
        _repository = repository;
        _usuarioLogado = usuarioLogado;
    }

    public async Task<Result<ResultMetaFinanceiraDTO>> Adicionar(CreateMetaFinanceiraDTO dto)
    {
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure<ResultMetaFinanceiraDTO>(
                Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var categoria = (CategoriaIconeMeta)dto.Categoria;

        var meta = new Domain.Entity.MetaFinanceira(
            dto.Nome, dto.ValorAlvo, dto.DataLimite,
            categoria, _usuarioLogado.UsuarioContextoDados);

        await _repository.Add(meta);

        return Result.Success(MapearParaDTO(meta));
    }

    public async Task<Result<ResultMetaFinanceiraDTO>> Atualizar(UpdateMetaFinanceiraDTO dto)
    {
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure<ResultMetaFinanceiraDTO>(
                Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var meta = await _repository.GetById(dto.Id);
        if (meta == null)
            return Result.Failure<ResultMetaFinanceiraDTO>(Error.NotFound("Meta financeira não encontrada."));

        var categoria = (CategoriaIconeMeta)dto.Categoria;
        meta.Atualizar(dto.Nome, dto.ValorAlvo, dto.DataLimite, categoria);

        await _repository.Update(meta);

        return Result.Success(MapearParaDTO(meta));
    }

    public async Task<Result> Excluir(string id)
    {
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var meta = await _repository.GetById(id);
        if (meta == null)
            return Result.Failure(Error.NotFound("Meta financeira não encontrada."));

        await _repository.Delete(meta);
        return Result.Success();
    }

    public async Task<Result<ResultMetaFinanceiraDTO>> ObterPeloID(string id)
    {
        var meta = await _repository.GetById(id);
        if (meta == null)
            return Result.Failure<ResultMetaFinanceiraDTO>(Error.NotFound("Meta financeira não encontrada."));

        return Result.Success(MapearParaDTO(meta));
    }

    public async Task<List<ResultMetaFinanceiraDTO>> ObterTodas()
    {
        var metas = await _repository.ObterPorUsuario(_usuarioLogado.IdContextoDados);
        return metas.Select(MapearParaDTO).ToList();
    }

    public async Task<ResumoMetasDTO> ObterResumo()
    {
        var metas = await _repository.ObterPorUsuario(_usuarioLogado.IdContextoDados);

        return new ResumoMetasDTO
        {
            TotalMetas = metas.Sum(m => m.ValorAlvo),
            TotalInvestido = metas.Sum(m => m.ValorAtual),
            PercentualGeral = metas.Sum(m => m.ValorAlvo) > 0
                ? (metas.Sum(m => m.ValorAtual) / metas.Sum(m => m.ValorAlvo)) * 100
                : 0,
            MetasConcluidas = metas.Count(m => m.Concluida),
            TotalDeMetasAtivas = metas.Count
        };
    }

    public async Task<Result<ResultContribuicaoDTO>> AdicionarContribuicao(string metaId, ContribuicaoDTO dto)
    {
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure<ResultContribuicaoDTO>(
                Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var meta = await _repository.GetById(metaId);
        if (meta == null)
            return Result.Failure<ResultContribuicaoDTO>(Error.NotFound("Meta financeira não encontrada."));

        var notificacao = meta.AdicionarContribuicao(dto.Valor, dto.Data, dto.InvestimentoId, dto.NomeInvestimento);

        await _repository.Update(meta);

        var resultado = new ResultContribuicaoDTO
        {
            MetaAtualizada = MapearParaDTO(meta),
            Notificacao = notificacao != null ? new NotificacaoMetaDTO
            {
                Tipo = notificacao.Tipo.ToString(),
                Mensagem = notificacao.Mensagem
            } : null
        };

        return Result.Success(resultado);
    }

    public async Task<Result> RemoverContribuicao(string metaId, string contribuicaoId)
    {
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var meta = await _repository.GetById(metaId);
        if (meta == null)
            return Result.Failure(Error.NotFound("Meta financeira não encontrada."));

        meta.RemoverContribuicao(contribuicaoId);
        await _repository.Update(meta);

        return Result.Success();
    }

    private ResultMetaFinanceiraDTO MapearParaDTO(Domain.Entity.MetaFinanceira meta)
    {
        return new ResultMetaFinanceiraDTO
        {
            Id = meta.Id,
            Nome = meta.Nome,
            ValorAlvo = meta.ValorAlvo,
            DataLimite = meta.DataLimite,
            Categoria = meta.Categoria,
            ValorAtual = meta.ValorAtual,
            PercentualProgresso = Math.Round(meta.PercentualProgresso, 1),
            Concluida = meta.Concluida,
            DiasRestantes = Math.Max(0, (meta.DataLimite - DateTime.Today).Days),
            ValorFaltante = Math.Max(0, meta.ValorAlvo - meta.ValorAtual),
            Contribuicoes = meta.Contribuicoes
                .OrderByDescending(c => c.Data)
                .Select(c => new ContribuicaoResultDTO
                {
                    Id = c.Id,
                    Valor = c.Valor,
                    Data = c.Data,
                    InvestimentoId = c.InvestimentoId,
                    NomeInvestimento = c.NomeInvestimento,
                    Origem = c.Origem.ToString()
                }).ToList(),
            DataCriacao = meta.DataCriacao
        };
    }
}
