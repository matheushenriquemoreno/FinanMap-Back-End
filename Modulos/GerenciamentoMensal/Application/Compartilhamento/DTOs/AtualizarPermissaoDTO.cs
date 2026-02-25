using Domain.Compartilhamento.Entity;

namespace Application.Compartilhamento.DTOs;

public class AtualizarPermissaoDTO
{
    public string CompartilhamentoId { get; set; }
    public NivelPermissao NovaPermissao { get; set; }
}
