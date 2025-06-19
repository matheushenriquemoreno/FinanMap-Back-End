using Application.DTOs;
using Application.Interfaces;
using Domain.Enum;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controlles;

public static class Categoria
{
    public static RouteGroupBuilder MapCategoriaEndpoints(this IEndpointRouteBuilder enpointRouteBuilder)
    {
        var group = enpointRouteBuilder.MapGroup("/api/Categorias");


        group.MapGet("/{id:length(24)}", async (string id, ICategoriaService service) =>
        {
            var result = await service.ObterPeloID(id);

            return result.MapResult();
        });

        group.MapGet("/GetUserCategorias", async (
            [FromQuery] TipoCategoria tipoCategoria,
            [FromQuery] string nome,
            ICategoriaService service) =>
        {
            var result = await service.ObterCategoria(tipoCategoria, nome);

            return result.MapResult();
        });


        group.MapGet("/Apoio/SugestoesCategoria", (
             [FromQuery] TipoCategoria tipoCategoria,
             [FromQuery] string nomeItemCadastro,
             ISugestaoCategoria service) =>
        {
            var result = service.ObterSurgestoesDeCategoriaBaseadoNoItemACadastrar(tipoCategoria, nomeItemCadastro);

            return result.MapResult();
        });

        group.MapPost("/", async (CreateCategoriaDTO categoriadto, ICategoriaService service) =>
        {
            var result = await service.Adicionar(categoriadto);

            return result.MapResultCreated();
        });

        group.MapPut("/", async (UpdateCategoriaDTO categoriadto, ICategoriaService service) =>
        {
            var result = await service.Atualizar(categoriadto);

            return result.MapResult();
        });

        group.MapDelete("/{id:length(24)}", async (string id, ICategoriaService service) =>
        {
            var result = await service.Excluir(id);

            return result.MapResult();
        });

        return group;
    }

}
