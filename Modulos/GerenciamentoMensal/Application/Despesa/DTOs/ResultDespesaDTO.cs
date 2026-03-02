using Application.Shared.DTOs;

namespace Application.DTOs;

public class ResultDespesaDTO : ResultTransacaoDTO
{
    public bool? EhDespesaAgrupadora { get; set; }
    public string IdDespesaAgrupadora { get; set; }
    public ResultDespesaDTO Agrupadora { get; set; }

    public string DespesaOrigemId { get; set; }
    public bool IsParcelado { get; set; }
    public bool IsRecorrente { get; set; }
    public int? ParcelaAtual { get; set; }
    public int? TotalParcelas { get; set; }

    public string DescricaoECategoria
    {
        get
        {
            return $"{Descricao} - {CategoriaNome}";
        }
    }
}
