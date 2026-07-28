#nullable enable

using System.ComponentModel;
using System.Security.Claims;
using Application.Mcp.Models;
using ModelContextProtocol.Server;

namespace WebApi.Mcp;

[McpServerToolType]
public sealed class McpImportTools(
    IMcpImportService service,
    IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool(
        Name = "finanmap_import_preview",
        Title = "Preparar importação financeira estruturada",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpImportPreviewData>))]
    [Description(
        "Valida itens JSON estruturados e persiste uma prévia de importação sem alterar dados financeiros.")]
    public Task<McpToolEnvelope<McpImportPreviewData>> PrepareAsync(
        [Description("Identificador idempotente único desta solicitação.")]
        string requestId,
        [Description("Até 1000 itens estruturados; documentos, arquivos e base64 não são aceitos.")]
        IReadOnlyList<McpImportItemInput> items,
        CancellationToken cancellationToken = default) =>
        service.PrepareAsync(Context(), requestId, items, cancellationToken);

    [McpServerTool(
        Name = "finanmap_import_confirm",
        Title = "Confirmar itens válidos da importação",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpImportStatusData>))]
    [Description(
        "Executa parcialmente e uma única vez os itens válidos da prévia usando a decisão exata IMPORT_VALID_ITEMS.")]
    public Task<McpToolEnvelope<McpImportStatusData>> ConfirmAsync(
        [Description("Identificador opaco do lote retornado pela prévia.")]
        string batchId,
        [Description("Hash integral retornado pela mesma prévia.")]
        string payloadHash,
        [Description("Decisão exata exigida pela prévia.")]
        McpImportConfirmationDecision decision,
        CancellationToken cancellationToken = default) =>
        service.ConfirmAsync(
            Context(),
            batchId,
            payloadHash,
            decision,
            cancellationToken);

    [McpServerTool(
        Name = "finanmap_import_status_get",
        Title = "Consultar status da importação",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpImportStatusData>))]
    [Description(
        "Consulta o estado conhecido de um lote próprio sem repetir efeitos concluídos.")]
    public Task<McpToolEnvelope<McpImportStatusData>> GetStatusAsync(
        [Description("Identificador opaco do lote próprio.")]
        string batchId,
        CancellationToken cancellationToken = default) =>
        service.GetStatusAsync(Context(), batchId, cancellationToken);

    [McpServerTool(
        Name = "finanmap_import_correction_preview",
        Title = "Preparar correção de importação",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpImportPreviewData>))]
    [Description(
        "Prepara uma nova prévia apenas para itens corrigíveis de um lote anterior, sem repetir itens concluídos.")]
    public Task<McpToolEnvelope<McpImportPreviewData>> PrepareCorrectionAsync(
        [Description("Identificador idempotente único desta solicitação.")]
        string requestId,
        [Description("Identificador opaco do lote pai próprio.")]
        string parentBatchId,
        [Description("Itens corrigidos, mantendo os mesmos clientItemId do lote pai.")]
        IReadOnlyList<McpImportItemInput> items,
        CancellationToken cancellationToken = default) =>
        service.PrepareCorrectionAsync(
            Context(),
            requestId,
            parentBatchId,
            items,
            cancellationToken);

    private McpCallContext Context()
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
        return new McpCallContext(
            userId,
            connectionId,
            correlationId,
            ClientId: clientId);
    }
}
