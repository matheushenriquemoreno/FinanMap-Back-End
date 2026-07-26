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
    public async Task Official_sdk_discovers_all_read_tools_over_streamable_http_2025_11_25()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools<McpCategoriesTool>()
            .WithTools<McpFinancialTools>();
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

        var names = tools.Select(tool => tool.Name).Order().ToArray();
        Assert.Equal(
            new[]
            {
                "finanmap_categories_list",
                "finanmap_category_impact_get",
                "finanmap_expenses_list",
                "finanmap_financial_summary_get",
                "finanmap_fixed_costs_list",
                "finanmap_incomes_list",
                "finanmap_investments_list",
                "finanmap_largest_movements_get",
                "finanmap_periods_compare"
            },
            names);
        Assert.All(tools, tool =>
        {
            Assert.True(tool.ProtocolTool.Annotations?.ReadOnlyHint);
            Assert.False(tool.ProtocolTool.Annotations?.DestructiveHint);
            Assert.False(tool.ProtocolTool.Annotations?.OpenWorldHint);
            Assert.NotNull(tool.ProtocolTool.OutputSchema);
        });
        var largestSchema = tools
            .Single(tool => tool.Name == "finanmap_largest_movements_get")
            .ProtocolTool.InputSchema
            .ToString();
        Assert.Contains("\"Income\"", largestSchema);
        Assert.Contains("\"Expense\"", largestSchema);
        Assert.DoesNotContain("\"Investment\"", largestSchema);
        var compareSchema = tools
            .Single(tool => tool.Name == "finanmap_periods_compare")
            .ProtocolTool.InputSchema
            .ToString();
        Assert.Contains("metrics", compareSchema);
        Assert.Contains("\"Totals\"", compareSchema);
        Assert.Contains("\"Difference\"", compareSchema);
        Assert.Contains("\"Percentage\"", compareSchema);
    }
}
