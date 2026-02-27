namespace Application.MetaFinanceira.DTOs;

public class UpdateMetaFinanceiraDTO
{
    public string Id { get; set; }
    public string Nome { get; set; }
    public decimal ValorAlvo { get; set; }
    public DateTime DataLimite { get; set; }
    public int Categoria { get; set; }
}
