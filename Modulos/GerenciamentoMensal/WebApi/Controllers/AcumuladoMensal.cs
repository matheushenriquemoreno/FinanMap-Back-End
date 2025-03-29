using Application.Reports.DTOs;
using Application.Reports.Interface;
using Domain.Relatorios;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers
{
    public static class AcumuladoMensal
    {
        public static RouteGroupBuilder MapAcumuladoMensalEndpoints(this IEndpointRouteBuilder enpointRouteBuilder)
        {
            var group = enpointRouteBuilder.MapGroup("/api/AcumuladoMensalReport");

            group.MapGet("/AcumuladoMensal", async (
                [FromQuery] int mes,
                [FromQuery] int ano,
                [FromQuery] TipoTransacao? tipo,
                IAcumuladoMensalReportService service) =>
            {
                AcumuladoMensalReportDTO result = await service.ObterReport(mes, ano, tipo);

                return Results.Ok(result);
            });

            return group;
        }
    }
}
