using Application.CustoFixo.DTOs;
using Application.CustoFixo.Interfaces;
using Domain.Compartilhamento.Entity;
using Domain.Entity;
using Domain.Enum;
using Domain.Login.Interfaces;
using Domain.Repository;

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

        await _custoFixoRepository.Add(custoFixo);

        return Result.Success(CustoFixoResponseDTO.Mapear(custoFixo));
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

        return Result.Success(CustoFixoResponseDTO.Mapear(custoFixo));
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

        return Result.Success(custosFixos.Select(CustoFixoResponseDTO.Mapear).ToList());
    }

    public async Task<Result<CustoFixoResponseDTO>> ObterPeloID(string id)
    {
        var custoFixo = await ObterCustoFixoDoUsuario(id);
        if (custoFixo is null)
            return Result.Failure<CustoFixoResponseDTO>(Error.NotFound("Custo fixo informado não existe!"));

        return Result.Success(CustoFixoResponseDTO.Mapear(custoFixo));
    }

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

    private async Task<Result> ValidarCategoria(string categoriaId)
    {
        if (string.IsNullOrWhiteSpace(categoriaId))
            return Result.Success();

        Categoria categoria = await _categoriaRepository.GetById(categoriaId);

        if (categoria is null || categoria.UsuarioId != _usuarioLogado.IdContextoDados || categoria.Tipo != TipoCategoria.Despesa)
            return Result.Failure(Error.NotFound("Categoria informada não existe!"));

        return Result.Success();
    }
}
