using Domain.Entity;

namespace Application.CompraPlanejada.DTOs;

public class CompraPlanejadaResponseDTO
{
    public string Id { get; set; }
    public string Nome { get; set; }
    public decimal ValorEstimado { get; set; }
    public PrioridadeCompraPlanejada Prioridade { get; set; }
    public string Descricao { get; set; }
    public List<CompraPlanejadaLinkDTO> LinksLojas { get; set; } = [];
    public EstadoCompraPlanejada Estado { get; set; }
    public DateTime DataCriacao { get; set; }
    public decimal? ValorReal { get; set; }
    public DateTime? DataCompra { get; set; }
    public string DespesaId { get; set; }

    public static CompraPlanejadaResponseDTO Mapear(Domain.Entity.CompraPlanejada compra)
    {
        return new CompraPlanejadaResponseDTO
        {
            Id = compra.Id,
            Nome = compra.Nome,
            ValorEstimado = compra.ValorEstimado,
            Prioridade = compra.Prioridade,
            Descricao = compra.Descricao,
            LinksLojas = compra.LinksLojas
                .Select(link => new CompraPlanejadaLinkDTO
                {
                    Url = link.Url,
                    NomeLoja = link.NomeLoja
                })
                .ToList(),
            Estado = compra.Estado,
            DataCriacao = compra.DataCriacao,
            ValorReal = compra.ValorReal,
            DataCompra = compra.DataCompra,
            DespesaId = compra.DespesaId
        };
    }
}
