using System.Security.Claims;
using System.ComponentModel;
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

    [Fact]
    public void Write_preview_tools_are_structured_idempotent_and_non_destructive()
    {
        var tools = ToolMethods(typeof(McpWriteTools))
            .Where(item => item.Attribute.Name?.EndsWith("_preview", StringComparison.Ordinal) == true)
            .OrderBy(item => item.Attribute.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "finanmap_category_create_preview",
                "finanmap_category_delete_preview",
                "finanmap_category_update_preview",
                "finanmap_income_create_preview",
                "finanmap_income_delete_preview",
                "finanmap_income_update_preview"
            },
            tools.Select(item => item.Attribute.Name).ToArray());
        Assert.All(tools, item =>
        {
            Assert.False(item.Attribute.ReadOnly);
            Assert.False(item.Attribute.Destructive);
            Assert.True(item.Attribute.Idempotent);
            Assert.False(item.Attribute.OpenWorld);
            Assert.True(item.Attribute.UseStructuredContent);
            Assert.NotNull(item.Attribute.OutputSchemaType);
            Assert.False(string.IsNullOrWhiteSpace(item.Attribute.Title));
            Assert.NotEmpty(
                item.Method.GetCustomAttributes(typeof(DescriptionAttribute), false));
            Assert.DoesNotContain(
                item.Method.GetParameters(),
                parameter => parameter.Name is
                    "userId" or
                    "usuarioId" or
                    "proprietarioId");
        });
    }

    [Fact]
    public void Confirmation_cancel_and_status_tools_have_conservative_annotations()
    {
        var tools = ToolMethods(typeof(McpWriteTools))
            .ToDictionary(item => item.Attribute.Name!, StringComparer.Ordinal);

        Assert.Equal(9, tools.Count);

        var confirm = tools["finanmap_operation_confirm"].Attribute;
        Assert.False(confirm.ReadOnly);
        Assert.True(confirm.Destructive);
        Assert.True(confirm.Idempotent);
        Assert.False(confirm.OpenWorld);
        Assert.True(confirm.UseStructuredContent);

        var cancel = tools["finanmap_operation_cancel"].Attribute;
        Assert.False(cancel.ReadOnly);
        Assert.False(cancel.Destructive);
        Assert.True(cancel.Idempotent);
        Assert.False(cancel.OpenWorld);
        Assert.True(cancel.UseStructuredContent);

        var status = tools["finanmap_operation_status_get"].Attribute;
        Assert.True(status.ReadOnly);
        Assert.False(status.Destructive);
        Assert.True(status.Idempotent);
        Assert.False(status.OpenWorld);
        Assert.True(status.UseStructuredContent);
    }

    [Fact]
    public async Task Write_tools_reject_legacy_owner_header_even_with_oauth_subject()
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
        var tool = new McpWriteTools(
            null!,
            new HttpContextAccessor { HttpContext = context });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            tool.PrepareCategoryDeleteAsync("request-a", "category-a"));
    }

    private static IEnumerable<(
        System.Reflection.MethodInfo Method,
        McpServerToolAttribute Attribute)> ToolMethods(Type type) =>
        type.GetMethods()
            .Select(method => (
                Method: method,
                Attribute: method.GetCustomAttributes(
                        typeof(McpServerToolAttribute),
                        false)
                    .OfType<McpServerToolAttribute>()
                    .SingleOrDefault()))
            .Where(item => item.Attribute is not null)
            .Select(item => (item.Method, item.Attribute!));
}
