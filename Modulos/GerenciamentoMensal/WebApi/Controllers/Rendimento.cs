using Application.DTOs;
using Application.Interface;
using Application.Interfaces;
using Application.Shared.Transacao.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers
{
    public static class Rendimento
    {
        public static RouteGroupBuilder MapRendimentoEndpoints(this IEndpointRouteBuilder enpointRouteBuilder)
        {
            var group = enpointRouteBuilder.MapGroup("/api/Rendimentos");

            group.MapGet("/{id:length(24)}", async (string id, IRendimentoService service) =>
            {
                var result = await service.ObterPeloID(id);

                return result.MapResult();
            });

            group.MapGet("/", async ([FromQuery] int mes, [FromQuery] int ano, IRendimentoService service) =>
            {
                var result = await service.ObterRendimentoMes(mes, ano);

                return Results.Ok(result);
            });

            group.MapPost("/", async (CreateRendimentoDTO rendimentoDTO, IRendimentoService service) =>
            {
                var result = await service.Adicionar(rendimentoDTO);

                return result.MapResultCreated();
            });

            group.MapPut("/", async (UpdateRendimentoDTO rendimentoDTO, IRendimentoService service) =>
            {
                var result = await service.Atualizar(rendimentoDTO);

                return result.MapResult();
            });

            group.MapPatch("/UpdateValor", async (UpdateValorTransacaoDTO rendimentoDTO, IRendimentoService service) =>
            {
                var result = await service.AtualizarValor(rendimentoDTO);

                return result.MapResult();
            });

            group.MapDelete("/{id:length(24)}", async (string id, IRendimentoService service) =>
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
                    resultados.Add(result);
                }

                return resultados.MapResult();
            });

            return group;
        }
    }
}
