namespace Application.MetaFinanceira.DTOs;

public class UpdateContribuicaoDTO
{
    public string ContribuicaoId { get; set; }
    public decimal Valor { get; set; }
    public string? Descricao { get; set; }
}
