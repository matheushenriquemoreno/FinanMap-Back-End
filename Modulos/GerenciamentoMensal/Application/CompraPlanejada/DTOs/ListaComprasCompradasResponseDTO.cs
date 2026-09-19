namespace Application.CompraPlanejada.DTOs;

public class ListaComprasCompradasResponseDTO
{
    public List<CompraPlanejadaResponseDTO> Itens { get; set; } = [];
    public decimal TotalEstimado { get; set; }
    public decimal TotalReal { get; set; }
}
