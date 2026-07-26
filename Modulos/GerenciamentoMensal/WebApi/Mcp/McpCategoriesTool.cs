using System.ComponentModel;
using System.Security.Claims;
using Application.Mcp.Models;
using Application.Mcp.Services;
using Domain.Enum;
using ModelContextProtocol.Server;

namespace WebApi.Mcp;

[McpServerToolType]
public sealed class McpCategoriesTool(
    McpCategoriesToolService service,
    IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool(
        Name = "finanmap_categories_list",
        Title = "Consultar categorias",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpCategoriesData>))]
    [Description("Lista somente as categorias financeiras da conta individual autenticada.")]
    public Task<McpToolEnvelope<McpCategoriesData>> ListAsync(
        [Description("Tipo opcional: Despesa, Rendimento ou Investimento.")]
        TipoCategoria? tipo = null,
        [Description("Texto opcional contido no nome da categoria.")]
        string? text = null,
        [Description("Quantidade máxima entre 1 e 200.")]
        int limit = 50,
        [Description("Cursor opaco retornado pela página anterior.")]
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var context = httpContextAccessor.HttpContext
            ?? throw new UnauthorizedAccessException("Contexto HTTP MCP indisponível.");
        if (context.Request.Headers.ContainsKey("X-Proprietario-Id"))
            throw new UnauthorizedAccessException("Contexto compartilhado não é aceito pelo MCP.");

        var userId = context.User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("Subject MCP ausente.");
        var connectionId = context.User.FindFirstValue(McpClaimNames.ConnectionId)
            ?? throw new UnauthorizedAccessException("Conexão MCP ausente.");
        var clientId = context.User.FindFirstValue("client_id") ?? string.Empty;
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId))
            correlationId = context.TraceIdentifier;

        return service.ExecuteAsync(
            new McpCallContext(userId, connectionId, correlationId, ClientId: clientId),
            new McpCategoriesInput(tipo, text, limit, cursor),
            cancellationToken);
    }
}

public static class McpClaimNames
{
    public const string ConnectionId = "mcp_connection_id";
    public const string AuthorizationId = "mcp_authorization_id";
}
