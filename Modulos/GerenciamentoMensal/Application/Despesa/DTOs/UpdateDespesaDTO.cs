using Application.Shared.Transacao.DTOs;

namespace Application.DTOs;

public class UpdateDespesaDTO : UpdateTransacaoDTO
{
    public string IdDespesaAgrupadora
    {
        get; set;
    }

}