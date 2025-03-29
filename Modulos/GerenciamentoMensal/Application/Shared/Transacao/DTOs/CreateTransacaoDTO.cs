namespace Application.Shared.DTOs;

public class CreateTransacaoDTO
{
    public int Ano { get; set; }
    public int Mes { get; set; }
    public string Descricao { get; set; }
    public decimal Valor { get; set; }
    public string CategoriaId { get; set; }
}
