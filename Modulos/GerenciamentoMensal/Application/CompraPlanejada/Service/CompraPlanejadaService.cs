using Application.CompraPlanejada.DTOs;
using Application.CompraPlanejada.Interfaces;
using Domain.Compartilhamento.Entity;
using Domain.Entity;
using Domain.Exceptions;
using Domain.Login.Interfaces;
using Domain.Repository;

namespace Application.CompraPlanejada.Service;

public class CompraPlanejadaService : ICompraPlanejadaService
{
    private readonly ICompraPlanejadaRepository _repository;
    private readonly IUsuarioLogado _usuarioLogado;

    public CompraPlanejadaService(
        ICompraPlanejadaRepository repository,
        IUsuarioLogado usuarioLogado)
    {
        _repository = repository;
        _usuarioLogado = usuarioLogado;
    }

    public async Task<Result<CompraPlanejadaResponseDTO>> Adicionar(CreateCompraPlanejadaDTO createDTO)
    {
        if (!PodeEditar())
            return Result.Failure<CompraPlanejadaResponseDTO>(
                Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        try
        {
            var links = CriarLinks(createDTO.LinksLojas);

            var compra = new Domain.Entity.CompraPlanejada(
                _usuarioLogado.IdContextoDados,
                createDTO.Nome,
                createDTO.ValorEstimado,
                createDTO.Prioridade,
                createDTO.Descricao,
                links);

            await _repository.Add(compra);

            return Result.Success(CompraPlanejadaResponseDTO.Mapear(compra));
        }
        catch (DomainValidatorException exception)
        {
            return Result.Failure<CompraPlanejadaResponseDTO>(
                Error.Validation(string.Join(" ", exception.Errors)));
        }
    }

    public async Task<Result<ListaComprasPlanejadasResponseDTO>> ListarPendentes()
    {
        var compras = await _repository.GetPendentes(_usuarioLogado.IdContextoDados);

        return Result.Success(new ListaComprasPlanejadasResponseDTO
        {
            Itens = compras.Select(CompraPlanejadaResponseDTO.Mapear).ToList(),
            TotalEstimado = compras.Sum(compra => compra.ValorEstimado)
        });
    }

    public async Task<Result<CompraPlanejadaResponseDTO>> Atualizar(UpdateCompraPlanejadaDTO updateDTO)
    {
        if (!PodeEditar())
            return Result.Failure<CompraPlanejadaResponseDTO>(
                Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var compra = await _repository.GetById(updateDTO.Id, _usuarioLogado.IdContextoDados);
        if (compra is null)
            return Result.Failure<CompraPlanejadaResponseDTO>(
                Error.NotFound("Compra planejada informada não existe."));

        if (compra.Estado != EstadoCompraPlanejada.Pendente)
            return Result.Failure<CompraPlanejadaResponseDTO>(
                Error.Validation("Somente itens pendentes podem ser alterados."));

        try
        {
            var links = CriarLinks(updateDTO.LinksLojas);

            compra.Atualizar(
                updateDTO.Nome,
                updateDTO.ValorEstimado,
                updateDTO.Prioridade,
                updateDTO.Descricao,
                links);

            await _repository.Update(compra);

            return Result.Success(CompraPlanejadaResponseDTO.Mapear(compra));
        }
        catch (DomainValidatorException exception)
        {
            return Result.Failure<CompraPlanejadaResponseDTO>(
                Error.Validation(string.Join(" ", exception.Errors)));
        }
    }

    public async Task<Result> Excluir(string id)
    {
        if (!PodeEditar())
            return Result.Failure(
                Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var compra = await _repository.GetById(id, _usuarioLogado.IdContextoDados);
        if (compra is null)
            return Result.Failure(Error.NotFound("Compra planejada informada não existe."));

        if (compra.Estado != EstadoCompraPlanejada.Pendente)
            return Result.Failure(Error.Validation("Somente itens pendentes podem ser excluídos."));

        await _repository.Delete(compra);

        return Result.Success();
    }

    private bool PodeEditar()
    {
        return !_usuarioLogado.EmModoCompartilhado
            || _usuarioLogado.PermissaoAtual == NivelPermissao.Editar;
    }

    private static List<LinkLojaCompraPlanejada> CriarLinks(
        IEnumerable<CompraPlanejadaLinkDTO> links)
    {
        return links?.Select(link =>
        {
            if (link is null)
                throw new DomainValidatorException("A lista de links da loja contém item inválido.");

            return new LinkLojaCompraPlanejada(link.Url, link.NomeLoja);
        }).ToList() ?? [];
    }
}
