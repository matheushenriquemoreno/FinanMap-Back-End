using Application.Reports.DTOs;
using Domain.Relatorios;

namespace Application.Reports.Interface
{
    public interface IAcumuladoMensalReportService
    {
        Task<AcumuladoMensalReportDTO> ObterReport(int mes, int ano, TipoTransacao? TipoTransacao);
    }
}
