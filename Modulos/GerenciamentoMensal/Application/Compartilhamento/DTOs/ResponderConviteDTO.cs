namespace Application.Compartilhamento.DTOs;

public class ResponderConviteDTO
{
    public string CompartilhamentoId { get; set; }
    public bool Aceitar { get; set; } // true = Aceitar, false = Recusar
}
