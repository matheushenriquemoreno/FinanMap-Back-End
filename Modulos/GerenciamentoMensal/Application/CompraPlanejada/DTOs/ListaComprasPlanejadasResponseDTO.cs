namespace Application.CompraPlanejada.DTOs;

public class ListaComprasPlanejadasResponseDTO
{
    public List<CompraPlanejadaResponseDTO> Itens { get; set; } = [];
    public decimal TotalEstimado { get; set; }
}
