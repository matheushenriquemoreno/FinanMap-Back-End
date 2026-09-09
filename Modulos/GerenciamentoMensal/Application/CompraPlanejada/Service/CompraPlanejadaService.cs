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
            var links = createDTO.LinksLojas?
                .Select(link => new LinkLojaCompraPlanejada(link.Url, link.NomeLoja))
                .ToList() ?? [];

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

    private bool PodeEditar()
    {
        return !_usuarioLogado.EmModoCompartilhado
            || _usuarioLogado.PermissaoAtual == NivelPermissao.Editar;
    }
}
