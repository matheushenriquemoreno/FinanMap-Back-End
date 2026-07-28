using Application.Mcp.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WebApi.Mcp;
using Xunit;

namespace Tests;

public class McpRequestSecurityMiddlewareTests
{
    [Fact]
    public async Task Invalid_origin_is_rejected_without_calling_transport()
    {
        var called = false;
        var middleware = CreateMiddleware(() => called = true, options =>
        {
            options.EndpointEnabled = true;
            options.AllowedOrigins = ["https://agent.example"];
        });
        var context = CreateContext();
        context.Request.Headers.Origin = "https://evil.example";

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(called);
    }

    [Fact]
    public async Task Shared_context_header_is_always_rejected()
    {
        var called = false;
        var middleware = CreateMiddleware(() => called = true, options => options.EndpointEnabled = true);
        var context = CreateContext();
        context.Request.Headers["X-Proprietario-Id"] = "owner-b";

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.False(called);
    }

    [Fact]
    public async Task Non_browser_client_may_omit_origin()
    {
        var called = false;
        var middleware = CreateMiddleware(() => called = true, options => options.EndpointEnabled = true);
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.True(called);
    }

    [Fact]
    public async Task Disabled_endpoint_is_not_exposed()
    {
        var called = false;
        var middleware = CreateMiddleware(() => called = true, _ => { });
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.False(called);
    }

    [Fact]
    public async Task Authentication_challenge_references_protected_resource_metadata()
    {
        var options = new McpFeatureOptions
        {
            EndpointEnabled = true,
            PublicBaseUrl = "https://api.example"
        };
        var middleware = new McpRequestSecurityMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            Options.Create(options),
            new HostEnvironmentFake(),
            new McpTelemetry(),
            NullLogger<McpRequestSecurityMiddleware>.Instance);
        var context = CreateContext();

        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        Assert.Contains(
            "resource_metadata=\"https://api.example/.well-known/oauth-protected-resource/mcp\"",
            context.Response.Headers.WWWAuthenticate.ToString());
    }

    [Fact]
    public async Task Import_transport_rejects_multipart_before_the_mcp_handler()
    {
        var called = false;
        var middleware = CreateMiddleware(
            () => called = true,
            options => options.EndpointEnabled = true);
        var context = CreateContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "multipart/form-data; boundary=test";

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, context.Response.StatusCode);
        Assert.False(called);
    }

    [Theory]
    [InlineData("application/jsonp")]
    [InlineData("application/json-seq")]
    [InlineData("text/json")]
    public async Task Transport_rejects_non_json_media_types_without_prefix_matching(
        string contentType)
    {
        var called = false;
        var middleware = CreateMiddleware(
            () => called = true,
            options => options.EndpointEnabled = true);
        var context = CreateContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = contentType;

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, context.Response.StatusCode);
        Assert.False(called);
    }

    [Fact]
    public async Task Transport_rejects_unbounded_body_before_the_mcp_handler()
    {
        var called = false;
        var middleware = CreateMiddleware(
            () => called = true,
            options => options.EndpointEnabled = true);
        var context = CreateContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/json";
        context.Request.ContentLength =
            McpRequestSecurityMiddleware.MaximumTransportBodyBytes + 1;

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        Assert.False(called);
    }

    [Fact]
    public async Task Transport_propagates_a_bounded_correlation_id_on_every_response()
    {
        var middleware = CreateMiddleware(
            () => { },
            options => options.EndpointEnabled = true);
        var context = CreateContext();
        context.Request.Headers["X-Correlation-Id"] = new string('x', 200);

        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        var correlationId = context.Response.Headers["X-Correlation-Id"].ToString();
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
        Assert.InRange(correlationId.Length, 1, 128);
        Assert.Equal(correlationId, context.TraceIdentifier);
    }

    private static McpRequestSecurityMiddleware CreateMiddleware(
        Action next,
        Action<McpFeatureOptions> configure)
    {
        var options = new McpFeatureOptions();
        configure(options);
        return new McpRequestSecurityMiddleware(
            _ =>
            {
                next();
                return Task.CompletedTask;
            },
            Options.Create(options),
            new HostEnvironmentFake(),
            new McpTelemetry(),
            NullLogger<McpRequestSecurityMiddleware>.Instance);
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/mcp";
        context.Request.Scheme = "https";
        return context;
    }

    private sealed class HostEnvironmentFake : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
