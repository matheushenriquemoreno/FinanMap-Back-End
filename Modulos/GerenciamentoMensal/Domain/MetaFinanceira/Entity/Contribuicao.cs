#nullable enable
namespace Domain.Entity;

public class Contribuicao
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public decimal Valor { get; set; }
    public DateTime Data { get; set; }

    // Referência fraca ao investimento (o investimento não sabe disso)
    public string? InvestimentoId { get; set; }
    public string? NomeInvestimento { get; set; }
    public OrigemContribuicao Origem { get; set; } = OrigemContribuicao.Manual;

    protected Contribuicao() { }

    public Contribuicao(decimal valor, DateTime data)
    {
        Valor = valor;
        Data = data;
    }
}
