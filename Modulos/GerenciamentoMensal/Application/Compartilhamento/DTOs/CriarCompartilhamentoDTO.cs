using Domain.Compartilhamento.Entity;

namespace Application.Compartilhamento.DTOs;

public class CriarCompartilhamentoDTO
{
    public string ConvidadoEmail { get; set; }    // E-mail da pessoa a ser convidada
    public NivelPermissao Permissao { get; set; } // Nível de permissão: Visualizar ou Editar
}
