using Application.Shared.DTOs;

namespace Application.DTOs;

public class CreateDespesaDTO : CreateTransacaoDTO
{
    public string IdDespesaAgrupadora { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string DespesaOrigemId { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsParcelado { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsRecorrente { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public int? ParcelaAtual { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public int? TotalParcelas { get; set; }
}
