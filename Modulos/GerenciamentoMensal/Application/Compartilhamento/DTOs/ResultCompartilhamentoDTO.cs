using Domain.Compartilhamento.Entity;

namespace Application.Compartilhamento.DTOs;

public class ResultCompartilhamentoDTO
{
    public string Id { get; set; }
    public string ProprietarioId { get; set; }
    public string ProprietarioEmail { get; set; }
    public string ProprietarioNome { get; set; }
    public string ConvidadoId { get; set; }
    public string ConvidadoEmail { get; set; }
    public NivelPermissao Permissao { get; set; }
    public StatusConvite Status { get; set; }
    public DateTime DataCriacao { get; set; }
}
