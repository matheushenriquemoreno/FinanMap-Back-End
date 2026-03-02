namespace Application.MetaFinanceira.DTOs;

public class CreateMetaFinanceiraDTO
{
    public string Nome { get; set; }
    public decimal ValorAlvo { get; set; }
    public DateTime DataLimite { get; set; }
    public int Categoria { get; set; } // CategoriaIconeMeta enum value
}
