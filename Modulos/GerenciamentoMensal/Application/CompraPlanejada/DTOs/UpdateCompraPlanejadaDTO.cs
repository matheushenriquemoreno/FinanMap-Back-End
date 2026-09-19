using Domain.Entity;

namespace Application.CompraPlanejada.DTOs;

public class UpdateCompraPlanejadaDTO
{
    public string Id { get; set; }
    public string Nome { get; set; }
    public decimal ValorEstimado { get; set; }
    public PrioridadeCompraPlanejada Prioridade { get; set; }
    public string Descricao { get; set; }
    public List<CompraPlanejadaLinkDTO> LinksLojas { get; set; } = [];
}
