using Application.Dashboard.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

public static class Dashboard
{
    public static RouteGroupBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard");

        group.MapGet("/resumo", async (
            [FromQuery] string dataInicial,
            [FromQuery] string dataFinal,
            IDashboardService service) =>
        {

            var result = await service.ObterResumoFinanceiro(dataInicial, dataFinal);
            return result.MapResult();
        })
        .WithName("ObterResumoFinanceiro")
        .WithOpenApi(operation => new(operation)
        {
            Summary = "Obtém resumo financeiro do período",
            Description = "Retorna totais e tendências de Rendimentos, Despesas e Investimentos para o período especificado"
        });

        group.MapGet("/evolucao", async (
            [FromQuery] string dataInicial,
            [FromQuery] string dataFinal,
            IDashboardService service) =>
        {
            var result = await service.ObterEvolucaoPeriodo(dataInicial, dataFinal);
            return result.MapResult();
        })
        .WithName("ObterEvolucaoPeriodo")
        .WithOpenApi(operation => new(operation)
        {
            Summary = "Obtém evolução por período",
            Description = "Retorna dados agrupados por semanas (mês único) ou meses (múltiplos meses) para gráfico de evolução"
        });

        group.MapGet("/categorias", async (
            [FromQuery] string dataInicial,
            [FromQuery] string dataFinal,
            [FromQuery] string? tipo,
            IDashboardService service) =>
        {
            var result = await service.ObterDistribuicaoCategorias(dataInicial, dataFinal, tipo);
            return result.MapResult();
        })
        .WithName("ObterDistribuicaoCategorias")
        .WithOpenApi(operation => new(operation)
        {
            Summary = "Obtém distribuição por categorias",
            Description = "Retorna valores agrupados por categoria. Use o parâmetro 'tipo' para filtrar por Rendimento, Despesa ou Investimento"
        });

        return group;
    }
}
