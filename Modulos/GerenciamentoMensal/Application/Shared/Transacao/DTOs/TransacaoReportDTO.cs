using Domain.Relatorios.Entity;

namespace Application.Shared.Transacao.DTOs;

public class TransacaoReportDTO
{
    public decimal ValorRendimento { get; set; }
    public decimal ValorInvestimentos { get; set; }
    public decimal ValorDespesas { get; set; }
    public decimal ValorFinal { get; private set; }

    public static implicit operator TransacaoReportDTO?(AcumuladoMensalReport? reportAcumulado)
    {
        if (reportAcumulado == null)
            return default;

        return new TransacaoReportDTO()
        {
            ValorDespesas = reportAcumulado.ValorDespesas,
            ValorInvestimentos = reportAcumulado.ValorInvestimentos,
            ValorRendimento = reportAcumulado.ValorRendimento,
            ValorFinal = reportAcumulado.ValorFinal
        };
    }
}
