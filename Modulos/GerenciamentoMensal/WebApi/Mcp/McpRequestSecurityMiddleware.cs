using Application.Mcp.Configuration;
using Microsoft.Extensions.Options;

namespace WebApi.Mcp;

public sealed class McpRequestSecurityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptions<McpFeatureOptions> _options;
    private readonly IHostEnvironment _environment;

    public McpRequestSecurityMiddleware(
        RequestDelegate next,
        IOptions<McpFeatureOptions> options,
        IHostEnvironment environment)
    {
        _next = next;
        _options = options;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var options = _options.Value;
        if (!options.EndpointEnabled)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (context.Request.Headers.ContainsKey("X-Proprietario-Id"))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                code = "SHARED_CONTEXT_FORBIDDEN",
                message = "O MCP aceita somente a conta individual autenticada."
            });
            return;
        }

        if (!_environment.IsDevelopment() && !context.Request.IsHttps)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                code = "HTTPS_REQUIRED",
                message = "O endpoint MCP exige HTTPS."
            });
            return;
        }

        var origin = context.Request.Headers.Origin.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(origin) &&
            !options.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                code = "ORIGIN_FORBIDDEN",
                message = "A origem informada não está autorizada."
            });
            return;
        }

        context.Response.OnStarting(() =>
        {
            AddProtectedResourceChallenge(context, options);
            return Task.CompletedTask;
        });

        await _next(context);
        if (!context.Response.HasStarted)
            AddProtectedResourceChallenge(context, options);
    }

    private static void AddProtectedResourceChallenge(
        HttpContext context,
        McpFeatureOptions options)
    {
        if (context.Response.StatusCode != StatusCodes.Status401Unauthorized)
            return;

        var baseUrl = options.PublicBaseUrl.TrimEnd('/');
        var metadataUrl = $"{baseUrl}/.well-known/oauth-protected-resource/mcp";
        var challenge = $"Bearer resource_metadata=\"{metadataUrl}\"";
        var existing = context.Response.Headers.WWWAuthenticate.ToString();
        if (!existing.Contains("resource_metadata=", StringComparison.OrdinalIgnoreCase))
            context.Response.Headers.Append("WWW-Authenticate", challenge);
    }
}
