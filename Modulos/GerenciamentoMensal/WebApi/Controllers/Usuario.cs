using Application.DTOs;
using Application.Interface;

namespace WebApi.Controllers
{
    public static class Usuario
    {
        public static RouteGroupBuilder MapUsuarioEndpoints(this IEndpointRouteBuilder enpointRouteBuilder)
        {
            var group = enpointRouteBuilder.MapGroup("/api/User");

            group.MapGet("", (IServiceUsuario serviceUsuario) =>
            {
                return Results.Ok(serviceUsuario.ObterUsuarioLogado());
            });

            // Mapeando a nova rota configurada para opt-out global de Custos Fixos
            var configGroup = enpointRouteBuilder.MapGroup("/api/usuarios/configuracoes/custos-fixos")
                .WithTags("Usuario")
                .WithOpenApi()
                .RequireAuthorization();

            configGroup.MapGet("", async (IServiceUsuario serviceUsuario) =>
            {
                var config = await serviceUsuario.ObterConfiguracaoCustoFixoAsync();
                return Results.Ok(config);
            });

            configGroup.MapPut("", async (CustoFixoConfiguracaoDTO dto, IServiceUsuario serviceUsuario) =>
            {
                var result = await serviceUsuario.AtualizarConfiguracaoCustoFixoAsync(dto);
                return result.MapResult();
            });

            return group;
        }
    }
}
