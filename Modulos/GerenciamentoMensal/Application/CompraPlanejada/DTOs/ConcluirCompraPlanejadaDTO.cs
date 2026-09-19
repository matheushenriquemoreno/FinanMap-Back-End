namespace Application.CompraPlanejada.DTOs;

public class ConcluirCompraPlanejadaDTO
{
    public decimal ValorReal { get; set; }
    public DateTime DataCompra { get; set; }
    public bool CriarDespesa { get; set; }
    public int Mes { get; set; }
    public int Ano { get; set; }
    public string CategoriaId { get; set; }
}
