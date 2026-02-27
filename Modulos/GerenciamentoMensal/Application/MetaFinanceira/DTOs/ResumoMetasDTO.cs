namespace Application.MetaFinanceira.DTOs;

public class ResumoMetasDTO
{
    public decimal TotalMetas { get; set; }         // Soma de todos os ValorAlvo
    public decimal TotalInvestido { get; set; }      // Soma de todos os ValorAtual
    public decimal PercentualGeral { get; set; }     // TotalInvestido / TotalMetas * 100
    public int MetasConcluidas { get; set; }
    public int TotalDeMetasAtivas { get; set; }
}
