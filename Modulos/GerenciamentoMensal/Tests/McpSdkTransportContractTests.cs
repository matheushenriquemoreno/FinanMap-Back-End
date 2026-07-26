using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using WebApi.Mcp;
using Xunit;

namespace Tests;

public class McpSdkTransportContractTests
{
    [Fact]
    public async Task Official_sdk_discovers_categories_tool_over_streamable_http_2025_11_25()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools<McpCategoriesTool>();
        await using var app = builder.Build();
        app.MapMcp("/mcp");
        await app.StartAsync();

        var httpClient = app.GetTestClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp
            },
            httpClient,
            loggerFactory: null,
            ownsHttpClient: false);
        await using var client = await McpClient.CreateAsync(
            transport,
            new McpClientOptions { ProtocolVersion = "2025-11-25" });

        var tools = await client.ListToolsAsync();

        var categories = Assert.Single(tools);
        Assert.Equal("finanmap_categories_list", categories.Name);
    }
}
