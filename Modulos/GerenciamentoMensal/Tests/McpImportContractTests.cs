using System.ComponentModel;
using Application.Mcp.Models;
using ModelContextProtocol.Server;
using WebApi.Mcp;
using Xunit;

namespace Tests;

public sealed class McpImportContractTests
{
    [Fact]
    public void MCP_72_88_import_tools_have_closed_structured_contracts_and_safe_annotations()
    {
        var tools = typeof(McpImportTools)
            .GetMethods()
            .Select(method => (
                Method: method,
                Attribute: method
                    .GetCustomAttributes(typeof(McpServerToolAttribute), false)
                    .OfType<McpServerToolAttribute>()
                    .SingleOrDefault()))
            .Where(item => item.Attribute is not null)
            .ToDictionary(item => item.Attribute!.Name!, StringComparer.Ordinal);

        Assert.Equal(
            new[]
            {
                "finanmap_import_confirm",
                "finanmap_import_correction_preview",
                "finanmap_import_preview",
                "finanmap_import_status_get"
            },
            tools.Keys.Order(StringComparer.Ordinal));
        Assert.All(tools.Values, item =>
        {
            Assert.True(item.Attribute!.UseStructuredContent);
            Assert.False(item.Attribute.OpenWorld);
            Assert.True(item.Attribute.Idempotent);
            Assert.NotNull(item.Attribute.OutputSchemaType);
            Assert.NotEmpty(
                item.Method.GetCustomAttributes(typeof(DescriptionAttribute), false));
            Assert.DoesNotContain(
                item.Method.GetParameters(),
                parameter => parameter.Name is
                    "userId" or
                    "usuarioId" or
                    "proprietarioId");
        });

        Assert.False(tools["finanmap_import_preview"].Attribute!.ReadOnly);
        Assert.False(tools["finanmap_import_preview"].Attribute!.Destructive);
        Assert.False(tools["finanmap_import_confirm"].Attribute!.Destructive);
        Assert.True(tools["finanmap_import_status_get"].Attribute!.ReadOnly);
        Assert.False(tools["finanmap_import_correction_preview"].Attribute!.Destructive);
    }

    [Fact]
    public void MCP_73_74_item_contract_is_typed_for_all_five_domains_without_document_fields()
    {
        var item = new McpImportItemInput(
            "item-1",
            McpImportEntityType.Expense,
            "Planilha1!A2",
            new McpImportItemData(
                Year: 2026,
                Month: 7,
                Description: "Mercado",
                Amount: "100.00",
                CategoryType: null,
                Name: null,
                DueDay: null,
                CategoryId: null,
                Active: null),
            "Alimentação",
            null);

        Assert.Equal("item-1", item.ClientItemId);
        Assert.Equal("Planilha1!A2", item.SourceRef);
        Assert.Equal(McpImportEntityType.Expense, item.Type);
        Assert.DoesNotContain(
            typeof(McpImportItemInput).GetProperties(),
            property => property.Name.Contains(
                "document",
                StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains(
                            "file",
                            StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains(
                            "base64",
                            StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            [
                McpImportEntityType.Category,
                McpImportEntityType.Income,
                McpImportEntityType.Expense,
                McpImportEntityType.Investment,
                McpImportEntityType.FixedCost
            ],
            Enum.GetValues<McpImportEntityType>());
        Assert.Equal(
            [McpImportDuplicateDecision.Skip, McpImportDuplicateDecision.ImportAnyway],
            Enum.GetValues<McpImportDuplicateDecision>());
        Assert.Equal(
            [McpImportConfirmationDecision.IMPORT_VALID_ITEMS],
            Enum.GetValues<McpImportConfirmationDecision>());
    }
}
