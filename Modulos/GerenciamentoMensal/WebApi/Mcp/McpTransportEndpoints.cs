using Application.Mcp.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using OpenIddict.Validation.AspNetCore;

namespace WebApi.Mcp;

public static class McpTransportEndpoints
{
    public static void UseMcpTransportSecurity(this WebApplication app)
    {
        app.UseWhen(
            context => context.Request.Path.StartsWithSegments("/mcp"),
            branch => branch
                .UseCors("mcp")
                .UseMiddleware<McpRequestSecurityMiddleware>());
    }

    public static void MapMcpTransport(this WebApplication app)
    {
        app.MapGet("/.well-known/oauth-protected-resource/mcp", (
            IOptions<McpFeatureOptions> options) =>
        {
            var baseUrl = options.Value.PublicBaseUrl.TrimEnd('/');
            return Results.Ok(new
            {
                resource = $"{baseUrl}/mcp",
                authorization_servers = new[] { baseUrl },
                scopes_supported = McpProtocolContract.FullManagementScopes,
                bearer_methods_supported = new[] { "header" }
            });
        }).AllowAnonymous();

        app.MapMcp("/mcp")
            .RequireRateLimiting("mcp-transport")
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
                Policy = "McpBearer"
            });
    }
}
