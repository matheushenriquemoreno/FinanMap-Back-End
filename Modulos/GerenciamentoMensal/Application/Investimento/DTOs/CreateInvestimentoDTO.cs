using Application.Shared.DTOs;

namespace Application.DTOs
{
    public class CreateInvestimentoDTO : CreateTransacaoDTO
    {
#nullable enable
        public string? MetaFinanceiraId { get; set; }
#nullable disable
    }
}
