using Application.CompraPlanejada.DTOs;
using Application.CompraPlanejada.Interfaces;

namespace WebApi.Controllers;

public static class CompraPlanejada
{
    public static RouteGroupBuilder MapCompraPlanejadaEndpoints(this IEndpointRouteBuilder endpointRouteBuilder)
    {
        var group = endpointRouteBuilder.MapGroup("/api/compras-planejadas");

        group.MapPost("/", async (CreateCompraPlanejadaDTO dto, ICompraPlanejadaService service) =>
        {
            var result = await service.Adicionar(dto);

            return result.MapResultCreated();
        });

        group.MapGet("/", async (ICompraPlanejadaService service) =>
        {
            var result = await service.ListarPendentes();

            return result.MapResult();
        });

        return group;
    }
}
