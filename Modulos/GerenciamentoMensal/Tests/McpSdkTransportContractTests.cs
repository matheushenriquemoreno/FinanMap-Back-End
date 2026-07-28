using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using WebApi.Mcp;
using Xunit;

namespace Tests;

public class McpSdkTransportContractTests
{
    [Fact]
    public async Task Official_sdk_discovers_only_read_tools_when_writes_are_disabled()
    {
        var tools = await DiscoverToolsAsync(writeToolsEnabled: false);

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
            Assert.False(string.IsNullOrWhiteSpace(tool.ProtocolTool.Title));
            Assert.False(string.IsNullOrWhiteSpace(tool.ProtocolTool.Description));
        });
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

    [Fact]
    public async Task Official_sdk_discovers_closed_write_tools_only_when_flag_is_enabled()
    {
        var tools = await DiscoverToolsAsync(writeToolsEnabled: true);
        var names = tools.Select(tool => tool.Name).Order().ToArray();

        Assert.Equal(
            new[]
            {
                "finanmap_categories_list",
                "finanmap_category_create_preview",
                "finanmap_category_delete_preview",
                "finanmap_category_impact_get",
                "finanmap_category_update_preview",
                "finanmap_expense_create_preview",
                "finanmap_expense_delete_preview",
                "finanmap_expense_update_preview",
                "finanmap_expenses_list",
                "finanmap_financial_summary_get",
                "finanmap_fixed_cost_create_preview",
                "finanmap_fixed_cost_delete_preview",
                "finanmap_fixed_cost_update_preview",
                "finanmap_fixed_costs_list",
                "finanmap_import_confirm",
                "finanmap_import_correction_preview",
                "finanmap_import_preview",
                "finanmap_import_status_get",
                "finanmap_income_create_preview",
                "finanmap_income_delete_preview",
                "finanmap_income_update_preview",
                "finanmap_incomes_list",
                "finanmap_investment_create_preview",
                "finanmap_investment_delete_preview",
                "finanmap_investment_update_preview",
                "finanmap_investments_list",
                "finanmap_largest_movements_get",
                "finanmap_operation_cancel",
                "finanmap_operation_confirm",
                "finanmap_operation_status_get",
                "finanmap_periods_compare"
            },
            names);

        var writeTools = tools
            .Where(tool =>
                tool.Name.Contains("_preview", StringComparison.Ordinal) ||
                tool.Name.StartsWith("finanmap_import_", StringComparison.Ordinal) ||
                tool.Name.StartsWith("finanmap_operation_", StringComparison.Ordinal))
            .ToDictionary(tool => tool.Name, StringComparer.Ordinal);
        Assert.Equal(22, writeTools.Count);
        Assert.All(writeTools.Values, tool =>
        {
            Assert.False(tool.ProtocolTool.Annotations?.OpenWorldHint);
            Assert.True(tool.ProtocolTool.Annotations?.IdempotentHint);
            Assert.NotNull(tool.ProtocolTool.OutputSchema);
            var schema = tool.ProtocolTool.InputSchema.ToString();
            Assert.Contains("\"additionalProperties\":false", schema);
            Assert.DoesNotContain("userId", schema, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("usuarioId", schema, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("proprietarioId", schema, StringComparison.OrdinalIgnoreCase);
        });

        foreach (var preview in writeTools
                     .Where(item => item.Key.EndsWith("_preview", StringComparison.Ordinal))
                     .Select(item => item.Value))
        {
            Assert.False(preview.ProtocolTool.Annotations?.ReadOnlyHint);
            Assert.False(preview.ProtocolTool.Annotations?.DestructiveHint);
        }

        var confirm = writeTools["finanmap_operation_confirm"];
        Assert.False(confirm.ProtocolTool.Annotations?.ReadOnlyHint);
        Assert.True(confirm.ProtocolTool.Annotations?.DestructiveHint);
        var confirmSchema = confirm.ProtocolTool.InputSchema.ToString();
        Assert.Contains("\"APPLY_CHANGES\"", confirmSchema);
        Assert.Contains("\"DELETE_PERMANENTLY\"", confirmSchema);
        Assert.DoesNotContain("\"IMPORT_VALID_ITEMS\"", confirmSchema);

        var importConfirm = writeTools["finanmap_import_confirm"];
        Assert.False(importConfirm.ProtocolTool.Annotations?.ReadOnlyHint);
        Assert.False(importConfirm.ProtocolTool.Annotations?.DestructiveHint);
        Assert.Contains(
            "\"IMPORT_VALID_ITEMS\"",
            importConfirm.ProtocolTool.InputSchema.ToString());

        var cancel = writeTools["finanmap_operation_cancel"];
        Assert.False(cancel.ProtocolTool.Annotations?.ReadOnlyHint);
        Assert.False(cancel.ProtocolTool.Annotations?.DestructiveHint);

        var status = writeTools["finanmap_operation_status_get"];
        Assert.True(status.ProtocolTool.Annotations?.ReadOnlyHint);
        Assert.False(status.ProtocolTool.Annotations?.DestructiveHint);
    }

    [Fact]
    public void Transport_endpoint_requires_the_dedicated_rate_limit_policy()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = "Development";
        builder.Services.AddMcpFinanceiro(
            builder.Configuration,
            builder.Environment);
        using var app = builder.Build();

        app.MapMcpTransport();

        var mcpEndpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => string.Equals(
                endpoint.RoutePattern.RawText?.Trim('/'),
                "mcp",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.NotEmpty(mcpEndpoints);
        Assert.All(mcpEndpoints, endpoint =>
        {
            var limiter = endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>();
            Assert.NotNull(limiter);
            Assert.Equal("mcp-transport", limiter.PolicyName);
        });
    }

    private static async Task<IList<McpClientTool>> DiscoverToolsAsync(
        bool writeToolsEnabled)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = "Development";
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Mcp:WriteToolsEnabled"] = writeToolsEnabled.ToString()
            });
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddMcpFinanceiro(
            builder.Configuration,
            builder.Environment);
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

        return await client.ListToolsAsync();
    }
}
