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

        group.MapPut("/{id:length(24)}", async (
            string id,
            UpdateCompraPlanejadaDTO dto,
            ICompraPlanejadaService service) =>
        {
            dto.Id = id;
            var result = await service.Atualizar(dto);

            return result.MapResult();
        });

        group.MapDelete("/{id:length(24)}", async (string id, ICompraPlanejadaService service) =>
        {
            var result = await service.Excluir(id);

            return result.MapResult();
        });

        group.MapGet("/", async (ICompraPlanejadaService service) =>
        {
            var result = await service.ListarPendentes();

            return result.MapResult();
        });

        return group;
    }
}
