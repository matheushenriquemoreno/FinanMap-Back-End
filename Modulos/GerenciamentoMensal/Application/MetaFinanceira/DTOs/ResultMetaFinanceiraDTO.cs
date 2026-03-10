#nullable enable
using Domain.Entity;

namespace Application.MetaFinanceira.DTOs;

public class ResultMetaFinanceiraDTO
{
    public string Id { get; set; }
    public string Nome { get; set; }
    public decimal ValorAlvo { get; set; }
    public DateTime DataLimite { get; set; }
    public CategoriaIconeMeta Categoria { get; set; }
    public decimal ValorAtual { get; set; }
    public decimal PercentualProgresso { get; set; }
    public bool Concluida { get; set; }
    public int DiasRestantes { get; set; }
    public decimal ValorFaltante { get; set; }
    public List<ContribuicaoResultDTO> Contribuicoes { get; set; } = new();
    public DateTime DataCriacao { get; set; }
}

public class ContribuicaoResultDTO
{
    public string Id { get; set; }
    public decimal Valor { get; set; }
    public DateTime Data { get; set; }
    public string? Descricao { get; set; }
    public string? InvestimentoId { get; set; }
    public string? NomeInvestimento { get; set; }
    public string Origem { get; set; }
}
