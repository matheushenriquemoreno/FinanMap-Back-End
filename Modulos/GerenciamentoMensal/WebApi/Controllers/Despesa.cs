using Application.DTOs;
using Application.Interface;
using Application.Interfaces;
using Application.Shared.Transacao.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

public static class Despesa
{
    public static RouteGroupBuilder MapDespesaEndpoints(this IEndpointRouteBuilder enpointRouteBuilder)
    {
        var group = enpointRouteBuilder.MapGroup("/api/Despesas");

        group.MapGet("/{id:length(24)}", async (string id, IDespesaService service) =>
        {
            var result = await service.ObterPeloID(id);

            return result.MapResult();
        });

        group.MapGet("/", async ([FromQuery] int mes, [FromQuery] int ano, IDespesaService service) =>
        {
            var result = await service.ObterMesAno(mes, ano);

            return Results.Ok(result);
        });

        group.MapPost("/", async (CreateDespesaDTO categoriadto, IDespesaService service) =>
        {
            var result = await service.Adicionar(categoriadto);

            return result.MapResultCreated();
        });

        group.MapPut("/", async (UpdateDespesaDTO categoriadto, IDespesaService service) =>
        {
            var result = await service.Atualizar(categoriadto);

            return result.MapResult();
        });

        group.MapPatch("/UpdateValor", async (UpdateValorTransacaoDTO rendimentoDTO, IDespesaService service) =>
        {
            var result = await service.AtualizarValor(rendimentoDTO);

            return result.MapResult();
        });


        group.MapDelete("/{id:length(24)}", async (string id, IDespesaService service) =>
        {
            var result = await service.Excluir(id);

            return result.MapResult();
        });

        group.MapPost("/DeleteMany", async (DeleteTransacoesDTO registros, IDespesaService service) =>
        {
            List<Result> resultados = new();

            foreach (var registro in registros.IdTransacoes)
            {
                Result result = await service.Excluir(registro);
                resultados.Add(result);
            }

            return resultados.MapResult();
        });

        return group;
    }
}
