using Application.CompraPlanejada.DTOs;

namespace Application.CompraPlanejada.Interfaces;

public interface ICompraPlanejadaService
{
    Task<Result<CompraPlanejadaResponseDTO>> Adicionar(CreateCompraPlanejadaDTO createDTO);
    Task<Result<CompraPlanejadaResponseDTO>> Atualizar(UpdateCompraPlanejadaDTO updateDTO);
    Task<Result<ListaComprasPlanejadasResponseDTO>> ListarPendentes();
}
