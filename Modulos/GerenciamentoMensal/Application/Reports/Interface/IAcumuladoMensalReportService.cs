using Application.Reports.DTOs;
using Domain;

namespace Application.Reports.Interface
{
    public interface IAcumuladoMensalReportService
    {
        Task<AcumuladoMensalReportDTO> ObterReport(int mes, int ano, TipoTransacao? TipoTransacao);
    }
}
