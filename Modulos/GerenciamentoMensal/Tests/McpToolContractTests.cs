using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;
using WebApi.Mcp;
using Xunit;

namespace Tests;

public class McpToolContractTests
{
    [Fact]
    public async Task Transport_tool_rejects_traditional_id_without_oauth_subject()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("Id", "traditional-user"),
                    new Claim(McpClaimNames.ConnectionId, "connection-a")
                ],
                "McpBearer"))
        };
        var tool = new McpCategoriesTool(
            null!,
            new HttpContextAccessor { HttpContext = context });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tool.ListAsync());
    }

    [Fact]
    public void Categories_tool_is_discoverable_read_only_and_structured()
    {
        var typeAttribute = Assert.Single(
            typeof(McpCategoriesTool).GetCustomAttributes(typeof(McpServerToolTypeAttribute), false));
        var method = Assert.Single(
            typeof(McpCategoriesTool).GetMethods(),
            item => item.GetCustomAttributes(typeof(McpServerToolAttribute), false).Length > 0);
        var tool = Assert.IsType<McpServerToolAttribute>(
            Assert.Single(method.GetCustomAttributes(typeof(McpServerToolAttribute), false)));

        Assert.NotNull(typeAttribute);
        Assert.Equal("finanmap_categories_list", tool.Name);
        Assert.True(tool.ReadOnly);
        Assert.False(tool.Destructive);
        Assert.True(tool.UseStructuredContent);
        Assert.DoesNotContain(
            method.GetParameters(),
            parameter => parameter.Name is "userId" or "usuarioId" or "proprietarioId");
    }

    [Fact]
    public async Task Financial_tools_reject_legacy_owner_header_even_with_oauth_subject()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("sub", "owner-a"),
                    new Claim(McpClaimNames.ConnectionId, "connection-a")
                ],
                "McpBearer"))
        };
        context.Request.Headers["X-Proprietario-Id"] = "owner-b";
        var tool = new McpFinancialTools(
            null!,
            new HttpContextAccessor { HttpContext = context });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            tool.ListIncomesAsync("2026-01", "2026-01"));
    }
}
