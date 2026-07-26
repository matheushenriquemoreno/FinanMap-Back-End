using System.ComponentModel;
using System.Security.Claims;
using Application.Mcp.Models;
using Application.Mcp.Services;
using Domain.Enum;
using Domain.Enums;
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
        Name = "finanmap_expense_create_preview",
        Title = "Preparar criação de despesa",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpPreviewData>))]
    [Description(
        "Valida e persiste uma prévia para criar uma despesa única, parcelada ou recorrente, sem alterar dados financeiros.")]
    public Task<McpToolEnvelope<McpPreviewData>> PrepareExpenseCreateAsync(
        [Description("Identificador idempotente único desta solicitação.")] string requestId,
        [Description("Ano financeiro da primeira despesa.")] int year,
        [Description("Mês financeiro inicial entre 1 e 12.")] int month,
        [Description("Descrição proposta para a despesa.")] string description,
        [Description("Valor positivo em BRL, como string com até duas casas decimais.")] string amount,
        [Description("Identificador opaco de uma categoria própria de despesa.")] string categoryId,
        [Description("Indica parcelamento do valor total.")] bool isInstallment = false,
        [Description("Indica recorrência mensal com o mesmo valor.")] bool isRecurring = false,
        [Description("Quantidade de meses, entre 2 e 24, quando parcelada ou recorrente.")] int? recurrenceCount = null,
        [Description("Despesa agrupadora própria opcional.")] string? groupingExpenseId = null,
        CancellationToken cancellationToken = default) =>
        service.PrepareExpenseCreateAsync(
            Context(),
            new McpExpenseCreatePreviewInput(
                requestId,
                year,
                month,
                description,
                amount,
                categoryId,
                isInstallment,
                isRecurring,
                recurrenceCount,
                groupingExpenseId),
            cancellationToken);

    [McpServerTool(
        Name = "finanmap_expense_update_preview",
        Title = "Preparar alteração de despesa",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpPreviewData>))]
    [Description(
        "Valida e persiste uma prévia para alterar uma despesa, exigindo o alcance do lote quando houver recorrência ou parcelas.")]
    public Task<McpToolEnvelope<McpPreviewData>> PrepareExpenseUpdateAsync(
        [Description("Identificador idempotente único desta solicitação.")] string requestId,
        [Description("Identificador opaco da despesa própria.")] string id,
        [Description("Nova descrição, quando houver alteração.")] string? description = null,
        [Description("Novo valor positivo em BRL, quando houver alteração.")] string? amount = null,
        [Description("Nova categoria própria de despesa, quando houver alteração.")] string? categoryId = null,
        [Description("Nova despesa agrupadora; string vazia remove o vínculo.")] string? groupingExpenseId = null,
        [Description("Alcance explícito no lote: apenas esta, esta e próximas ou todas.")] ModificadorLote? batchModifier = null,
        CancellationToken cancellationToken = default) =>
        service.PrepareExpenseUpdateAsync(
            Context(),
            new McpExpenseUpdatePreviewInput(
                requestId,
                id,
                description,
                amount,
                categoryId,
                groupingExpenseId,
                batchModifier),
            cancellationToken);

    [McpServerTool(
        Name = "finanmap_expense_delete_preview",
        Title = "Preparar exclusão definitiva de despesa",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpPreviewData>))]
    [Description(
        "Valida e persiste uma prévia irreversível para excluir uma despesa, com alcance explícito para parcelas ou recorrências.")]
    public Task<McpToolEnvelope<McpPreviewData>> PrepareExpenseDeleteAsync(
        [Description("Identificador idempotente único desta solicitação.")] string requestId,
        [Description("Identificador opaco da despesa própria.")] string id,
        [Description("Alcance explícito no lote: apenas esta, esta e próximas ou todas.")] ModificadorLote? batchModifier = null,
        CancellationToken cancellationToken = default) =>
        service.PrepareExpenseDeleteAsync(
            Context(),
            new McpExpenseDeletePreviewInput(requestId, id, batchModifier),
            cancellationToken);

    [McpServerTool(
        Name = "finanmap_investment_create_preview",
        Title = "Preparar criação de investimento",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpPreviewData>))]
    [Description(
        "Valida e persiste uma prévia para criar um investimento, sem alterar dados financeiros.")]
    public Task<McpToolEnvelope<McpPreviewData>> PrepareInvestmentCreateAsync(
        [Description("Identificador idempotente único desta solicitação.")] string requestId,
        [Description("Ano financeiro do investimento.")] int year,
        [Description("Mês financeiro entre 1 e 12.")] int month,
        [Description("Descrição proposta para o investimento.")] string description,
        [Description("Valor positivo em BRL, como string com até duas casas decimais.")] string amount,
        [Description("Identificador opaco de uma categoria própria de investimento.")] string categoryId,
        CancellationToken cancellationToken = default) =>
        service.PrepareInvestmentCreateAsync(
            Context(),
            new McpInvestmentCreatePreviewInput(
                requestId,
                year,
                month,
                description,
                amount,
                categoryId),
            cancellationToken);

    [McpServerTool(
        Name = "finanmap_investment_update_preview",
        Title = "Preparar alteração de investimento",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpPreviewData>))]
    [Description(
        "Valida e persiste uma prévia com os valores atuais e propostos para alterar um investimento.")]
    public Task<McpToolEnvelope<McpPreviewData>> PrepareInvestmentUpdateAsync(
        [Description("Identificador idempotente único desta solicitação.")] string requestId,
        [Description("Identificador opaco do investimento próprio.")] string id,
        [Description("Nova descrição, quando houver alteração.")] string? description = null,
        [Description("Novo valor positivo em BRL, quando houver alteração.")] string? amount = null,
        [Description("Nova categoria própria de investimento, quando houver alteração.")] string? categoryId = null,
        CancellationToken cancellationToken = default) =>
        service.PrepareInvestmentUpdateAsync(
            Context(),
            new McpInvestmentUpdatePreviewInput(
                requestId,
                id,
                description,
                amount,
                categoryId),
            cancellationToken);

    [McpServerTool(
        Name = "finanmap_investment_delete_preview",
        Title = "Preparar exclusão definitiva de investimento",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpPreviewData>))]
    [Description(
        "Valida e persiste uma prévia irreversível para excluir definitivamente um investimento.")]
    public Task<McpToolEnvelope<McpPreviewData>> PrepareInvestmentDeleteAsync(
        [Description("Identificador idempotente único desta solicitação.")] string requestId,
        [Description("Identificador opaco do investimento próprio.")] string id,
        CancellationToken cancellationToken = default) =>
        service.PrepareInvestmentDeleteAsync(
            Context(),
            new McpInvestmentDeletePreviewInput(requestId, id),
            cancellationToken);

    [McpServerTool(
        Name = "finanmap_fixed_cost_create_preview",
        Title = "Preparar criação de custo fixo",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpPreviewData>))]
    [Description(
        "Valida e persiste uma prévia para criar um custo fixo mensal, sem alterar dados financeiros.")]
    public Task<McpToolEnvelope<McpPreviewData>> PrepareFixedCostCreateAsync(
        [Description("Identificador idempotente único desta solicitação.")] string requestId,
        [Description("Nome proposto para o custo fixo.")] string name,
        [Description("Dia de vencimento entre 1 e 31.")] int dueDay,
        [Description("Categoria própria de despesa opcional.")] string? categoryId = null,
        CancellationToken cancellationToken = default) =>
        service.PrepareFixedCostCreateAsync(
            Context(),
            new McpFixedCostCreatePreviewInput(requestId, name, dueDay, categoryId),
            cancellationToken);

    [McpServerTool(
        Name = "finanmap_fixed_cost_update_preview",
        Title = "Preparar alteração de custo fixo",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpPreviewData>))]
    [Description(
        "Valida e persiste uma prévia com os valores atuais e propostos para alterar um custo fixo mensal.")]
    public Task<McpToolEnvelope<McpPreviewData>> PrepareFixedCostUpdateAsync(
        [Description("Identificador idempotente único desta solicitação.")] string requestId,
        [Description("Identificador opaco do custo fixo próprio.")] string id,
        [Description("Novo nome, quando houver alteração.")] string? name = null,
        [Description("Novo dia de vencimento, quando houver alteração.")] int? dueDay = null,
        [Description("Nova categoria própria; string vazia remove a categoria.")] string? categoryId = null,
        [Description("Novo estado ativo/inativo, quando houver alteração.")] bool? active = null,
        CancellationToken cancellationToken = default) =>
        service.PrepareFixedCostUpdateAsync(
            Context(),
            new McpFixedCostUpdatePreviewInput(
                requestId,
                id,
                name,
                dueDay,
                categoryId,
                active),
            cancellationToken);

    [McpServerTool(
        Name = "finanmap_fixed_cost_delete_preview",
        Title = "Preparar exclusão definitiva de custo fixo",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpPreviewData>))]
    [Description(
        "Valida e persiste uma prévia irreversível para excluir definitivamente um custo fixo mensal.")]
    public Task<McpToolEnvelope<McpPreviewData>> PrepareFixedCostDeleteAsync(
        [Description("Identificador idempotente único desta solicitação.")] string requestId,
        [Description("Identificador opaco do custo fixo próprio.")] string id,
        CancellationToken cancellationToken = default) =>
        service.PrepareFixedCostDeleteAsync(
            Context(),
            new McpFixedCostDeletePreviewInput(requestId, id),
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
