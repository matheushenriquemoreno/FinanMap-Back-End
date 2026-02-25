namespace Application.DTOs
{
    public class LancarDespesaLoteDTO
    {
        public string Descricao { get; set; }
        public decimal ValorTotal { get; set; }
        public string CategoriaId { get; set; }
        public int AnoInicial { get; set; }
        public int MesInicial { get; set; }

        public bool IsParcelado { get; set; }
        public int QuantidadeMeses { get; set; }
        public bool IsRecorrenteFixa { get; set; }
        public string IdDespesaAgrupadora { get; set; }
    }
}
