#nullable enable
namespace Application.MetaFinanceira.DTOs;

public class ContribuicaoDTO
{
    public decimal Valor { get; set; }
    public DateTime Data { get; set; }
    public string? InvestimentoId { get; set; }
    public string? NomeInvestimento { get; set; }
}
