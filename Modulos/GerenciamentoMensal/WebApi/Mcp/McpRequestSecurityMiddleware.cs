using System.Diagnostics;
using Application.Mcp.Configuration;
using Application.Mcp.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace WebApi.Mcp;

public sealed class McpRequestSecurityMiddleware
{
    public const long MaximumTransportBodyBytes =
        McpImportService.MaximumPayloadBytes + (64 * 1024);
    private readonly RequestDelegate _next;
    private readonly IOptions<McpFeatureOptions> _options;
    private readonly IHostEnvironment _environment;
    private readonly McpTelemetry _telemetry;
    private readonly ILogger<McpRequestSecurityMiddleware> _logger;

    public McpRequestSecurityMiddleware(
        RequestDelegate next,
        IOptions<McpFeatureOptions> options,
        IHostEnvironment environment,
        McpTelemetry telemetry,
        ILogger<McpRequestSecurityMiddleware> logger)
    {
        _next = next;
        _options = options;
        _environment = environment;
        _telemetry = telemetry;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var correlationId = ResolveCorrelationId(context);
        context.TraceIdentifier = correlationId;
        context.Response.Headers["X-Correlation-Id"] = correlationId;
        try
        {
            await InvokeCoreAsync(context);
        }
        finally
        {
            _telemetry.RecordTransportRequest(
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }

    private async Task InvokeCoreAsync(HttpContext context)
    {
        var options = _options.Value;
        if (!options.EndpointEnabled)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (context.Request.Headers.ContainsKey("X-Proprietario-Id"))
        {
            Denied(context, "SHARED_CONTEXT_FORBIDDEN");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                code = "SHARED_CONTEXT_FORBIDDEN",
                message = "O MCP aceita somente a conta individual autenticada."
            });
            return;
        }

        if (HttpMethods.IsPost(context.Request.Method))
        {
            var mediaType = context.Request.ContentType?
                .Split(';', 2, StringSplitOptions.TrimEntries)[0];
            if (!string.Equals(
                    mediaType,
                    "application/json",
                    StringComparison.OrdinalIgnoreCase))
            {
                Denied(context, "UNSUPPORTED_MEDIA_TYPE");
                context.Response.StatusCode =
                    StatusCodes.Status415UnsupportedMediaType;
                await context.Response.WriteAsJsonAsync(new
                {
                    code = "UNSUPPORTED_MEDIA_TYPE",
                    message = "O MCP aceita somente solicitações JSON estruturadas."
                });
                return;
            }

            if (context.Request.ContentLength > MaximumTransportBodyBytes)
            {
                Denied(context, "LIMIT_EXCEEDED");
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                await context.Response.WriteAsJsonAsync(new
                {
                    code = "LIMIT_EXCEEDED",
                    message = "A solicitação excede o limite de transporte; divida a importação em lotes menores."
                });
                return;
            }

            var bodySize = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (bodySize is { IsReadOnly: false })
                bodySize.MaxRequestBodySize = MaximumTransportBodyBytes;
        }

        if (!_environment.IsDevelopment() && !context.Request.IsHttps)
        {
            Denied(context, "HTTPS_REQUIRED");
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
            Denied(context, "ORIGIN_FORBIDDEN");
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

    private void Denied(HttpContext context, string reason)
    {
        _telemetry.RecordSecurityDenial(reason);
        _logger.LogWarning(
            "Solicitação MCP rejeitada. Reason={Reason} CorrelationId={CorrelationId}; payload omitido.",
            reason,
            context.TraceIdentifier);
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var candidate = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        if (IsSafeCorrelationId(candidate))
            return candidate!;
        if (IsSafeCorrelationId(context.TraceIdentifier))
            return context.TraceIdentifier;
        return Guid.NewGuid().ToString("N");
    }

    private static bool IsSafeCorrelationId(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 128 &&
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '_' or '.' or ':');

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
