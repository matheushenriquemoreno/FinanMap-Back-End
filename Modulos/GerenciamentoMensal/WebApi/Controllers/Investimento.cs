using Application.DTOs;
using Application.Interface;
using Application.Interfaces;
using Application.Shared.Transacao.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

public static class Investimento
{
    public static RouteGroupBuilder MapInvestimentoEndpoints(this IEndpointRouteBuilder enpointRouteBuilder)
    {
        var group = enpointRouteBuilder.MapGroup("/api/Investimentos");

        group.MapGet("/{id:length(24)}", async (string id, IInvestimentoService service) =>
        {
            var result = await service.ObterPeloID(id);

            return result.MapResult();
        });

        group.MapGet("/", async ([FromQuery] int mes, [FromQuery] int ano, IInvestimentoService service) =>
        {
            var result = await service.ObterMesAno(mes, ano);

            return Results.Ok(result);
        });

        group.MapPost("/", async (CreateInvestimentoDTO categoriadto, IInvestimentoService service) =>
        {
            var result = await service.Adicionar(categoriadto);

            return result.MapResultCreated();
        });

        group.MapPut("/", async (UpdateInvestimentoDTO categoriadto, IInvestimentoService service) =>
        {
            var result = await service.Atualizar(categoriadto);

            return result.MapResult();
        });

        group.MapPatch("/UpdateValor", async (UpdateValorTransacaoDTO rendimentoDTO, IInvestimentoService service) =>
        {
            var result = await service.AtualizarValor(rendimentoDTO);

            return result.MapResult();
        });

        group.MapDelete("/{id:length(24)}", async (string id, IInvestimentoService service) =>
        {
            var result = await service.Excluir(id);

            return result.MapResult();
        });

        group.MapPost("/DeleteMany", async (DeleteTransacoesDTO registros, IRendimentoService service) =>
        {
            List<Result> resultados = new();

            foreach (var registro in registros.IdTransacoes)
            {
                Result result = await service.Excluir(registro);
            }

            return resultados.MapResult();
        });

        return group;
    }
}
