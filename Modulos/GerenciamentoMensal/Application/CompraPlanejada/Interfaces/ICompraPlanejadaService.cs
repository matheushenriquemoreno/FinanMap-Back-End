using Application.CompraPlanejada.DTOs;

namespace Application.CompraPlanejada.Interfaces;

public interface ICompraPlanejadaService
{
    Task<Result<CompraPlanejadaResponseDTO>> Adicionar(CreateCompraPlanejadaDTO createDTO);
    Task<Result<CompraPlanejadaResponseDTO>> Atualizar(UpdateCompraPlanejadaDTO updateDTO);
    Task<Result<ListaComprasPlanejadasResponseDTO>> ListarPendentes();
    Task<Result<CompraPlanejadaResponseDTO>> Concluir(string id, ConcluirCompraPlanejadaDTO dto);
    Task<Result<ListaComprasCompradasResponseDTO>> ListarComprados();
    Task<Result<CompraPlanejadaResponseDTO>> Reverter(string id, ReverterCompraPlanejadaDTO dto);
    Task<Result> Excluir(string id);
}
