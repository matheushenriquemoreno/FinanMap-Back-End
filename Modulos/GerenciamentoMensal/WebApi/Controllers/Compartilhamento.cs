using Application.Compartilhamento.DTOs;
using Application.Compartilhamento.Interfaces;

namespace WebApi.Controllers
{
    public static class Compartilhamento
    {
        public static RouteGroupBuilder MapCompartilhamentoEndpoints(this IEndpointRouteBuilder endpointRouteBuilder)
        {
            var group = endpointRouteBuilder.MapGroup("/api/compartilhamento");

            // POST /api/compartilhamento — Criar convite de compartilhamento
            group.MapPost("", async (ICompartilhamentoService service, CriarCompartilhamentoDTO dto) =>
            {
                var result = await service.Convidar(dto);
                return result.MapResultCreated();
            });

            // GET /api/compartilhamento/meus — Listar meus compartilhamentos (onde sou o dono)
            group.MapGet("/meus", async (ICompartilhamentoService service) =>
            {
                var result = await service.ObterMeusCompartilhamentos();
                return Results.Ok(result);
            });

            // GET /api/compartilhamento/convites — Listar convites recebidos
            group.MapGet("/convites", async (ICompartilhamentoService service) =>
            {
                var result = await service.ObterConvitesRecebidos();
                return Results.Ok(result);
            });

            // POST /api/compartilhamento/responder — Aceitar ou recusar convite
            group.MapPost("/responder", async (ICompartilhamentoService service, ResponderConviteDTO dto) =>
            {
                var result = await service.ResponderConvite(dto);
                return result.MapResult();
            });

            // PUT /api/compartilhamento/permissao — Atualizar nível de permissão
            group.MapPut("/permissao", async (ICompartilhamentoService service, AtualizarPermissaoDTO dto) =>
            {
                var result = await service.AtualizarPermissao(dto);
                return result.MapResult();
            });

            // DELETE /api/compartilhamento/{id} — Revogar compartilhamento
            group.MapDelete("/{id:length(24)}", async (string id, ICompartilhamentoService service) =>
            {
                var result = await service.Revogar(id);
                return result.MapResult();
            });

            return group;
        }
    }
}
