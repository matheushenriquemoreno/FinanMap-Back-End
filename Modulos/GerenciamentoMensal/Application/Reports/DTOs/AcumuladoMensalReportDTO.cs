using Application.DTOs;
using Domain.Relatorios.Entity;

namespace Application.Reports.DTOs
{
    public class AcumuladoMensalReportDTO
    {
        public decimal ValorRendimento { get; private set; }
        public decimal ValorInvestimentos { get; private set; }
        public decimal ValorDespesas { get; private set; }
        public decimal ValorFinal { get; private set; }

        public List<ResultRendimentoDTO> Rendimentos { get; set; }
        public List<ResultDespesaDTO> Despesas { get; set; }
        public List<ResultInvestimentoDTO> Investimentos { get; set; }

        public static implicit operator AcumuladoMensalReportDTO(AcumuladoMensalReport reportAcumulado)
        {
            return new AcumuladoMensalReportDTO
            {
                ValorRendimento = reportAcumulado.ValorRendimento,
                ValorDespesas = reportAcumulado.ValorDespesas,
                ValorInvestimentos = reportAcumulado.ValorInvestimentos,
                ValorFinal = reportAcumulado.ValorFinal,
            };
        }
    }
}
