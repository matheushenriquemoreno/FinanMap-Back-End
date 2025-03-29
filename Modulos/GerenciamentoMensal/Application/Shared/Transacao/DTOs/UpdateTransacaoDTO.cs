namespace Application.Shared.Transacao.DTOs;

public class UpdateTransacaoDTO
{
    public string Id { get; set; }
    public string Descricao { get; set; }
    public decimal Valor { get; set; }
    public string CategoriaId { get; set; }
}
