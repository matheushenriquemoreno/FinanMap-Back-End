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

            return group;
        }
    }
}
