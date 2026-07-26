using System.ComponentModel;
using System.Security.Claims;
using Application.Mcp.Models;
using Application.Mcp.Services;
using Domain.Enum;
using ModelContextProtocol.Server;

namespace WebApi.Mcp;

[McpServerToolType]
public sealed class McpWriteTools(
    McpWriteService service,
    IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool(
        Name = "finanmap_category_create_preview",
        Title = "Preparar criação de categoria",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpPreviewData>))]
    [Description(
        "Valida e persiste uma prévia para criar uma categoria, sem alterar dados financeiros.")]
    public Task<McpToolEnvelope<McpPreviewData>> PrepareCategoryCreateAsync(
        [Description("Identificador idempotente único desta solicitação.")] string requestId,
        [Description("Nome proposto para a categoria.")] string name,
        [Description("Tipo financeiro da categoria.")] TipoCategoria type,
        CancellationToken cancellationToken = default) =>
        service.PrepareCategoryCreateAsync(
            Context(),
            new McpCategoryCreatePreviewInput(requestId, name, type),
            cancellationToken);

    [McpServerTool(
        Name = "finanmap_category_update_preview",
        Title = "Preparar alteração de categoria",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpPreviewData>))]
    [Description(
        "Valida e persiste uma prévia com os valores atuais e propostos para alterar uma categoria.")]
    public Task<McpToolEnvelope<McpPreviewData>> PrepareCategoryUpdateAsync(
        [Description("Identificador idempotente único desta solicitação.")] string requestId,
        [Description("Identificador opaco da categoria própria.")] string id,
        [Description("Novo nome proposto para a categoria.")] string name,
        CancellationToken cancellationToken = default) =>
        service.PrepareCategoryUpdateAsync(
            Context(),
            new McpCategoryUpdatePreviewInput(requestId, id, name),
            cancellationToken);

    [McpServerTool(
        Name = "finanmap_category_delete_preview",
        Title = "Preparar exclusão definitiva de categoria",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpPreviewData>))]
    [Description(
        "Valida e persiste uma prévia que destaca a irreversibilidade da exclusão de uma categoria.")]
    public Task<McpToolEnvelope<McpPreviewData>> PrepareCategoryDeleteAsync(
        [Description("Identificador idempotente único desta solicitação.")] string requestId,
        [Description("Identificador opaco da categoria própria.")] string id,
        CancellationToken cancellationToken = default) =>
        service.PrepareCategoryDeleteAsync(
            Context(),
            new McpCategoryDeletePreviewInput(requestId, id),
            cancellationToken);

    [McpServerTool(
        Name = "finanmap_income_create_preview",
        Title = "Preparar criação de receita",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpPreviewData>))]
    [Description(
        "Valida e persiste uma prévia para criar uma receita, sem alterar dados financeiros.")]
    public Task<McpToolEnvelope<McpPreviewData>> PrepareIncomeCreateAsync(
        [Description("Identificador idempotente único desta solicitação.")] string requestId,
        [Description("Ano financeiro da receita.")] int year,
        [Description("Mês financeiro entre 1 e 12.")] int month,
        [Description("Descrição proposta para a receita.")] string description,
        [Description("Valor positivo em BRL, como string com até duas casas decimais.")]
        string amount,
        [Description("Identificador opaco de uma categoria própria de receita.")]
        string categoryId,
        CancellationToken cancellationToken = default) =>
        service.PrepareIncomeCreateAsync(
            Context(),
            new McpIncomeCreatePreviewInput(
                requestId,
                year,
                month,
                description,
                amount,
                categoryId),
            cancellationToken);

    [McpServerTool(
        Name = "finanmap_income_update_preview",
        Title = "Preparar alteração de receita",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpPreviewData>))]
    [Description(
        "Valida e persiste uma prévia com os valores atuais e somente os campos propostos para alterar uma receita.")]
    public Task<McpToolEnvelope<McpPreviewData>> PrepareIncomeUpdateAsync(
        [Description("Identificador idempotente único desta solicitação.")] string requestId,
        [Description("Identificador opaco da receita própria.")] string id,
        [Description("Nova descrição, quando houver alteração.")] string? description = null,
        [Description("Novo valor positivo em BRL, quando houver alteração.")]
        string? amount = null,
        [Description("Nova categoria própria de receita, quando houver alteração.")]
        string? categoryId = null,
        CancellationToken cancellationToken = default) =>
        service.PrepareIncomeUpdateAsync(
            Context(),
            new McpIncomeUpdatePreviewInput(
                requestId,
                id,
                description,
                amount,
                categoryId),
            cancellationToken);

    [McpServerTool(
        Name = "finanmap_income_delete_preview",
        Title = "Preparar exclusão definitiva de receita",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpPreviewData>))]
    [Description(
        "Valida e persiste uma prévia que destaca a irreversibilidade da exclusão de uma receita.")]
    public Task<McpToolEnvelope<McpPreviewData>> PrepareIncomeDeleteAsync(
        [Description("Identificador idempotente único desta solicitação.")] string requestId,
        [Description("Identificador opaco da receita própria.")] string id,
        CancellationToken cancellationToken = default) =>
        service.PrepareIncomeDeleteAsync(
            Context(),
            new McpIncomeDeletePreviewInput(requestId, id),
            cancellationToken);

    [McpServerTool(
        Name = "finanmap_operation_confirm",
        Title = "Confirmar operação financeira",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpOperationData>))]
    [Description(
        "Confirma uma prévia específica de uso único. É classificada conservadoramente como destrutiva porque pode executar uma exclusão definitiva.")]
    public Task<McpToolEnvelope<McpOperationData>> ConfirmAsync(
        [Description("Identificador opaco retornado pela prévia.")] string previewId,
        [Description("Hash integral retornado pela mesma prévia.")] string payloadHash,
        [Description("Decisão exata exigida pela prévia.")]
        McpConfirmationDecision decision,
        CancellationToken cancellationToken = default) =>
        service.ConfirmAsync(
            Context(),
            new McpOperationConfirmInput(
                previewId,
                payloadHash,
                decision.ToString()),
            cancellationToken);

    [McpServerTool(
        Name = "finanmap_operation_cancel",
        Title = "Cancelar prévia de operação",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpOperationData>))]
    [Description(
        "Invalida de forma idempotente uma prévia ainda não executada, sem alterar dados financeiros.")]
    public Task<McpToolEnvelope<McpOperationData>> CancelAsync(
        [Description("Identificador opaco da prévia a cancelar.")] string previewId,
        CancellationToken cancellationToken = default) =>
        service.CancelAsync(
            Context(),
            new McpOperationCancelInput(previewId),
            cancellationToken);

    [McpServerTool(
        Name = "finanmap_operation_status_get",
        Title = "Consultar status de operação",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpOperationData>))]
    [Description(
        "Consulta com segurança o estado e o resultado conhecido de uma operação própria antes de qualquer nova tentativa.")]
    public Task<McpToolEnvelope<McpOperationData>> GetStatusAsync(
        [Description("Identificador opaco da operação própria.")] string operationId,
        CancellationToken cancellationToken = default) =>
        service.GetStatusAsync(
            Context(),
            new McpOperationStatusInput(operationId),
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

public enum McpConfirmationDecision
{
    APPLY_CHANGES,
    DELETE_PERMANENTLY
}
