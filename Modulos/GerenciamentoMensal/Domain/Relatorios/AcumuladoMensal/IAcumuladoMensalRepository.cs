using Domain.Relatorios.Entity;

namespace Domain.Relatorios.AcumuladoMensal;

public interface IAcumuladoMensalReportRepository
{
    Task<AcumuladoMensalReport> Obter(int mes, int ano, string idUsuario);
}
