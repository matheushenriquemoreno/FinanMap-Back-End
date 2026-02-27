using WebApi.Controllers;
using WebApi.Controlles;

namespace WebApi.Configs;

public static class EndpointConfiguration
{
    public static void MapPublicEndpoints(this WebApplication app)
    {
        app.MapLoginEndpoints()
            .WithTags("Login")
            .WithOpenApi();

        app.MapHealthChecks("/health");
    }

    public static void MapProtectedEndpoints(this WebApplication app)
    {
        app.MapCategoriaEndpoints()
            .WithTags("Categorias")
            .WithOpenApi()
            .RequireAuthorization();

        app.MapRendimentoEndpoints()
            .WithTags("Rendimentos")
            .WithOpenApi()
            .RequireAuthorization();

        app.MapDespesaEndpoints()
            .WithTags("Despesas")
            .WithOpenApi()
            .RequireAuthorization();

        app.MapInvestimentoEndpoints()
            .WithTags("Investimentos")
            .WithOpenApi()
            .RequireAuthorization();

        app.MapAcumuladoMensalEndpoints()
            .WithTags("AcumuladoMensalReport")
            .WithOpenApi()
            .RequireAuthorization();

        app.MapUsuarioEndpoints()
            .WithTags("Usuario")
            .WithOpenApi()
            .RequireAuthorization();

        app.MapReplicarTransacaoEndpoints()
            .WithTags("ReplicarTranscao")
            .WithOpenApi()
            .RequireAuthorization();

        app.MapDashboardEndpoints()
            .WithTags("Dashboard")
            .WithOpenApi()
            .RequireAuthorization();

        app.MapCompartilhamentoEndpoints()
            .WithTags("Compartilhamento")
            .WithOpenApi()
            .RequireAuthorization();

        app.MapMetaFinanceiraEndpoints()
            .WithTags("Metas Financeiras")
            .WithOpenApi()
            .RequireAuthorization();
    }
}
