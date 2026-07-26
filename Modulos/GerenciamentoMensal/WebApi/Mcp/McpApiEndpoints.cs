using Application.Mcp.Configuration;
using Application.Mcp.Services;
using Domain.Login.Interfaces;
using Domain.Mcp.Enums;
using Domain.Mcp.Repositories;
using Microsoft.Extensions.Options;

namespace WebApi.Mcp;

public static class McpApiEndpoints
{
    public static RouteGroupBuilder MapMcpApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/mcp")
            .RequireAuthorization()
            .AddEndpointFilter(async (context, next) =>
            {
                var httpContext = context.HttpContext;
                if (!httpContext.Request.Headers.ContainsKey("X-Proprietario-Id"))
                    return await next(context);

                return Results.BadRequest(new
                {
                    code = "SHARED_CONTEXT_FORBIDDEN",
                    message = "A integração MCP aceita somente a conta individual autenticada."
                });
            });

        group.MapGet("/configuration", (IOptions<McpFeatureOptions> options) =>
            Results.Ok(McpHttpDtoMapper.MapConfiguration(options.Value)));

        group.MapGet("/service-status", (IOptions<McpFeatureOptions> options) =>
            Results.Ok(new
            {
                endpointEnabled = options.Value.EndpointEnabled,
                writeToolsEnabled = options.Value.WriteToolsEnabled,
                historyEnabled = options.Value.HistoryEnabled
            }));

        group.MapGet("/connections", async (
            IUsuarioLogado user,
            McpConnectionService service,
            IMcpAuthorizationInteractionRepository interactions,
            IOptions<McpFeatureOptions> options,
            CancellationToken cancellationToken) =>
        {
            var connections = await service.ListAsync(user.Id, cancellationToken);
            return Results.Ok(new McpListResponse<McpConnectionDto>(
                connections.Select(McpHttpDtoMapper.Map).ToList()));
        });

        group.MapGet("/connections/{id}", async (
            string id,
            IUsuarioLogado user,
            McpConnectionService service,
            CancellationToken cancellationToken) =>
        {
            var connection = await service.GetAsync(id, user.Id, cancellationToken);
            return connection is null
                ? Results.NotFound()
                : Results.Ok(McpHttpDtoMapper.Map(connection));
        });

        group.MapPost("/connections/{id}/revoke", async (
            string id,
            McpRevokeRequest? request,
            HttpContext httpContext,
            IUsuarioLogado user,
            McpConnectionService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var connection = await service.RevokeAsync(
                    id,
                    user.Id,
                    request?.ReasonCode,
                    CorrelationId(httpContext),
                    cancellationToken);
                return Results.Ok(McpHttpDtoMapper.Map(connection));
            }
            catch (McpConnectionNotFoundException)
            {
                return Results.NotFound();
            }
        });

        group.MapGet("/audit-events", async (
            DateTime? fromUtc,
            DateTime? toUtc,
            McpOperationClass? operationClass,
            McpOperationState? status,
            string? cursor,
            int? limit,
            IUsuarioLogado user,
            IMcpAuditQueryRepository repository,
            IOptions<McpFeatureOptions> options,
            CancellationToken cancellationToken) =>
        {
            if (!options.Value.HistoryEnabled)
                return Results.NotFound();

            var page = await repository.ListOwnedAsync(
                user.Id,
                fromUtc,
                toUtc,
                operationClass,
                status,
                cursor,
                limit ?? 50,
                cancellationToken);
            return Results.Ok(new McpListResponse<McpAuditEventDto>(
                page.Items.Select(McpHttpDtoMapper.Map).ToList(),
                page.NextCursor));
        });

        group.MapGet("/audit-events/{id}", async (
            string id,
            IUsuarioLogado user,
            IMcpAuditQueryRepository repository,
            IOptions<McpFeatureOptions> options,
            CancellationToken cancellationToken) =>
        {
            if (!options.Value.HistoryEnabled)
                return Results.NotFound();

            var auditEvent = await repository.GetOwnedAsync(id, user.Id, cancellationToken);
            return auditEvent is null
                ? Results.NotFound()
                : Results.Ok(McpHttpDtoMapper.Map(auditEvent));
        });

        group.MapGet("/authorization-interactions/{id}", async (
            string id,
            IUsuarioLogado user,
            IMcpAuthorizationInteractionRepository repository,
            CancellationToken cancellationToken) =>
        {
            var interaction = await repository.GetAsync(id, cancellationToken);
            if (interaction is null)
                return Results.NotFound();

            try
            {
                interaction.Claim(user.Id, DateTime.UtcNow);
                await repository.UpdateAsync(interaction, cancellationToken);
                return Results.Ok(new
                {
                    interaction.Id,
                    interaction.ClientId,
                    interaction.ClientName,
                    requestedScopes = interaction.RequestedScopes,
                    status = interaction.Status.ToString().ToLowerInvariant(),
                    interaction.ExpiresAtUtc,
                    interaction.RedirectUri,
                    interaction.State
                });
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound();
            }
        });

        group.MapPost("/authorization-interactions/{id}/approve", async (
            string id,
            McpApproveRequest request,
            HttpContext httpContext,
            IUsuarioLogado user,
            McpConnectionService service,
            IMcpAuthorizationInteractionRepository interactions,
            IOptions<McpFeatureOptions> options,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var connection = await service.ApproveAsync(
                    id,
                    user.Id,
                    request.Scopes,
                    CorrelationId(httpContext),
                    cancellationToken);
                var interaction = await interactions.GetAsync(id, cancellationToken)
                    ?? throw new McpAuthorizationInteractionNotFoundException();
                httpContext.Response.Headers["X-Mcp-Authorization-Continue"] =
                    McpOAuthAuthorizeEndpoint.BuildContinuationUrl(
                        options.Value, connection, interaction);
                return Results.Ok(McpHttpDtoMapper.Map(connection));
            }
            catch (McpAuthorizationInteractionNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new
                {
                    code = "AUTHORIZATION_INTERACTION_INVALID",
                    message = exception.Message
                });
            }
        });

        group.MapPost("/authorization-interactions/{id}/deny", async (
            string id,
            HttpContext httpContext,
            IUsuarioLogado user,
            McpConnectionService service,
            IMcpAuthorizationInteractionRepository interactions,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await service.DenyAsync(
                    id, user.Id, CorrelationId(httpContext), cancellationToken);
                var interaction = await interactions.GetAsync(id, cancellationToken)
                    ?? throw new McpAuthorizationInteractionNotFoundException();
                httpContext.Response.Headers["X-Mcp-Authorization-Continue"] =
                    McpOAuthAuthorizeEndpoint.BuildDeniedContinuationUrl(interaction);
                return Results.NoContent();
            }
            catch (McpAuthorizationInteractionNotFoundException)
            {
                return Results.NotFound();
            }
        });

        return group;
    }

    private static string CorrelationId(HttpContext context)
    {
        var header = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        return string.IsNullOrWhiteSpace(header) ? context.TraceIdentifier : header;
    }
}
