using Domain.Enums;

namespace Application.DTOs
{
    public class AtualizarLoteDespesaDTO
    {
        public decimal NovoValor { get; set; }
        public string NovaDescricao { get; set; }
        public string NovaCategoriaId { get; set; }
        public ModificadorLote Modificador { get; set; }
    }
}
