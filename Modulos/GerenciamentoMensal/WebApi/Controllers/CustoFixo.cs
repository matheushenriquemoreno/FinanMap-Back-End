using Application.CustoFixo.DTOs;
using Application.CustoFixo.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

public static class CustoFixo
{
    public static RouteGroupBuilder MapCustoFixoEndpoints(this IEndpointRouteBuilder enpointRouteBuilder)
    {
        var group = enpointRouteBuilder.MapGroup("/api/custos-fixos");

        group.MapPost("/", async (CreateCustoFixoDTO dto, ICustoFixoService service) =>
        {
            var result = await service.Adicionar(dto);

            return result.MapResultCreated();
        });

        group.MapGet("/", async (ICustoFixoService service) =>
        {
            var result = await service.Listar();

            return result.MapResult();
        });

        group.MapPut("/{id:length(24)}", async (string id, UpdateCustoFixoDTO dto, ICustoFixoService service) =>
        {
            dto.Id = id;
            var result = await service.Atualizar(dto);

            return result.MapResult();
        });

        group.MapDelete("/{id:length(24)}", async (string id, ICustoFixoService service) =>
        {
            var result = await service.Excluir(id);

            return result.MapResult();
        });

        return group;
    }
}
