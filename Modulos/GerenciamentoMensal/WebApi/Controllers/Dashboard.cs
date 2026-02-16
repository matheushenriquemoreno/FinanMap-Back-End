using Application.Dashboard.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

public static class Dashboard
{
    public static RouteGroupBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard");

        group.MapGet("/resumo", async (
            [FromQuery] int mesInicial,
            [FromQuery] int mesFinal,
            [FromQuery] int ano,
            IDashboardService service) =>
        {

            var result = await service.ObterResumoFinanceiro(mesInicial, mesFinal, ano);
            return result.MapResult();
        })
        .WithName("ObterResumoFinanceiro")
        .WithOpenApi(operation => new(operation)
        {
            Summary = "Obtém resumo financeiro do período",
            Description = "Retorna totais e tendências de Rendimentos, Despesas e Investimentos para o período especificado"
        });

        group.MapGet("/evolucao", async (
            [FromQuery] int mesInicial,
            [FromQuery] int mesFinal,
            [FromQuery] int ano,
            IDashboardService service) =>
        {
            var result = await service.ObterEvolucaoPeriodo(mesInicial, mesFinal, ano);
            return result.MapResult();
        })
        .WithName("ObterEvolucaoPeriodo")
        .WithOpenApi(operation => new(operation)
        {
            Summary = "Obtém evolução por período",
            Description = "Retorna dados agrupados por semanas (mês único) ou meses (múltiplos meses) para gráfico de evolução"
        });

        group.MapGet("/categorias", async (
            [FromQuery] int mesInicial,
            [FromQuery] int mesFinal,
            [FromQuery] int ano,
            [FromQuery] string? tipo,
            IDashboardService service) =>
        {
            var result = await service.ObterDistribuicaoCategorias(mesInicial, mesFinal, ano, tipo);
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
