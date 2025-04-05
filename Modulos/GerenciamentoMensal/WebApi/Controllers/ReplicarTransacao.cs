using Application.ReplicarTransacao.DTOs;
using Application.ReplicarTransacao.Interfaces;

namespace WebApi.Controllers;

public static class ReplicarTransacao
{
    public static RouteGroupBuilder MapReplicarTransacaoEndpoints(this IEndpointRouteBuilder enpointRouteBuilder)
    {
        var group = enpointRouteBuilder.MapGroup("/api/ReplicarTransacao");

        group.MapPost("/Periodo", async (ReplicarTransacoesPeriodoDTO dto, IReplicarTransacaoService service) =>
        {
            var result = await service.ReplicarTransacaoPeriodo(dto);

            return result.MapResult();
        });

        return group;
    }
}
