using System.ComponentModel;
using System.Security.Claims;
using Application.Mcp.Models;
using Application.Mcp.Services;
using ModelContextProtocol.Server;

namespace WebApi.Mcp;

[McpServerToolType]
public sealed class McpFinancialTools(
    McpFinancialReadService service,
    IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool(
        Name = "finanmap_incomes_list",
        Title = "Consultar receitas",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpFinancialListData>))]
    [Description("Lista receitas próprias por período, filtros e cursor, sem exigir confirmação.")]
    public Task<McpToolEnvelope<McpFinancialListData>> ListIncomesAsync(
        [Description("Mês inicial inclusivo no formato YYYY-MM.")] string from,
        [Description("Mês final inclusivo no formato YYYY-MM.")] string to,
        [Description("ID ou nome exato da categoria.")] string? category = null,
        [Description("Texto contido na descrição.")] string? description = null,
        [Description("Quantidade entre 1 e 200; padrão 50.")] int limit = 50,
        [Description("Cursor opaco retornado pela página anterior.")] string? cursor = null,
        CancellationToken cancellationToken = default) =>
        service.ListAsync(
            Context(),
            McpFinancialKind.Income,
            new McpFinancialQueryInput(
                from, to, category, description, limit, cursor),
            cancellationToken);

    [McpServerTool(
        Name = "finanmap_expenses_list",
        Title = "Consultar despesas",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpFinancialListData>))]
    [Description("Lista despesas próprias por período, filtros e cursor, sem exigir confirmação.")]
    public Task<McpToolEnvelope<McpFinancialListData>> ListExpensesAsync(
        [Description("Mês inicial inclusivo no formato YYYY-MM.")] string from,
        [Description("Mês final inclusivo no formato YYYY-MM.")] string to,
        [Description("ID ou nome exato da categoria.")] string? category = null,
        [Description("Texto contido na descrição.")] string? description = null,
        [Description("Quantidade entre 1 e 200; padrão 50.")] int limit = 50,
        [Description("Cursor opaco retornado pela página anterior.")] string? cursor = null,
        CancellationToken cancellationToken = default) =>
        service.ListAsync(
            Context(),
            McpFinancialKind.Expense,
            new McpFinancialQueryInput(
                from, to, category, description, limit, cursor),
            cancellationToken);

    [McpServerTool(
        Name = "finanmap_investments_list",
        Title = "Consultar investimentos",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpFinancialListData>))]
    [Description("Lista investimentos próprios por período, filtros e cursor, sem exigir confirmação.")]
    public Task<McpToolEnvelope<McpFinancialListData>> ListInvestmentsAsync(
        [Description("Mês inicial inclusivo no formato YYYY-MM.")] string from,
        [Description("Mês final inclusivo no formato YYYY-MM.")] string to,
        [Description("ID ou nome exato da categoria.")] string? category = null,
        [Description("Texto contido na descrição.")] string? description = null,
        [Description("Quantidade entre 1 e 200; padrão 50.")] int limit = 50,
        [Description("Cursor opaco retornado pela página anterior.")] string? cursor = null,
        CancellationToken cancellationToken = default) =>
        service.ListAsync(
            Context(),
            McpFinancialKind.Investment,
            new McpFinancialQueryInput(
                from, to, category, description, limit, cursor),
            cancellationToken);

    [McpServerTool(
        Name = "finanmap_fixed_costs_list",
        Title = "Consultar custos fixos",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpFixedCostListData>))]
    [Description("Lista custos fixos próprios por status, categoria e cursor, sem exigir confirmação.")]
    public Task<McpToolEnvelope<McpFixedCostListData>> ListFixedCostsAsync(
        [Description("Status ativo opcional.")] bool? active = null,
        [Description("ID ou nome exato da categoria.")] string? category = null,
        [Description("Quantidade entre 1 e 200; padrão 50.")] int limit = 50,
        [Description("Cursor opaco retornado pela página anterior.")] string? cursor = null,
        CancellationToken cancellationToken = default) =>
        service.ListFixedCostsAsync(
            Context(),
            new McpFixedCostQueryInput(active, category, limit, cursor),
            cancellationToken);

    [McpServerTool(
        Name = "finanmap_financial_summary_get",
        Title = "Obter resumo financeiro",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpFinancialSummaryData>))]
    [Description("Obtém totais de receitas, despesas, investimentos e saldo no período.")]
    public Task<McpToolEnvelope<McpFinancialSummaryData>> GetFinancialSummaryAsync(
        [Description("Mês inicial inclusivo no formato YYYY-MM.")] string from,
        [Description("Mês final inclusivo no formato YYYY-MM.")] string to,
        CancellationToken cancellationToken = default) =>
        service.GetSummaryAsync(
            Context(), new McpPeriodInput(from, to), cancellationToken);

    [McpServerTool(
        Name = "finanmap_largest_movements_get",
        Title = "Obter maiores movimentos",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpLargestMovementsData>))]
    [Description("Obtém as maiores receitas ou despesas no período.")]
    public Task<McpToolEnvelope<McpLargestMovementsData>> GetLargestMovementsAsync(
        [Description("Tipo Income ou Expense.")] McpMovementKind kind,
        [Description("Mês inicial inclusivo no formato YYYY-MM.")] string from,
        [Description("Mês final inclusivo no formato YYYY-MM.")] string to,
        [Description("Quantidade entre 1 e 50; padrão 5.")] int quantity = 5,
        CancellationToken cancellationToken = default) =>
        service.GetLargestMovementsAsync(
            Context(),
            new McpLargestMovementsInput(kind, from, to, quantity),
            cancellationToken);

    [McpServerTool(
        Name = "finanmap_category_impact_get",
        Title = "Obter impacto por categoria",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpCategoryImpactData>))]
    [Description("Agrupa valores e percentuais por categoria no período.")]
    public Task<McpToolEnvelope<McpCategoryImpactData>> GetCategoryImpactAsync(
        [Description("Tipo Income, Expense ou Investment.")] McpFinancialKind kind,
        [Description("Mês inicial inclusivo no formato YYYY-MM.")] string from,
        [Description("Mês final inclusivo no formato YYYY-MM.")] string to,
        CancellationToken cancellationToken = default) =>
        service.GetCategoryImpactAsync(
            Context(),
            new McpCategoryImpactInput(kind, from, to),
            cancellationToken);

    [McpServerTool(
        Name = "finanmap_periods_compare",
        Title = "Comparar períodos financeiros",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpToolEnvelope<McpPeriodsCompareData>))]
    [Description("Compara totais, diferenças e percentual de saldo entre dois períodos.")]
    public Task<McpToolEnvelope<McpPeriodsCompareData>> ComparePeriodsAsync(
        [Description("Início do período A em YYYY-MM.")] string fromA,
        [Description("Fim do período A em YYYY-MM.")] string toA,
        [Description("Início do período B em YYYY-MM.")] string fromB,
        [Description("Fim do período B em YYYY-MM.")] string toB,
        [Description(
            "Métricas opcionais: Totals, Difference e Percentage; quando omitidas, retorna todas.")]
        McpComparisonMetric[]? metrics = null,
        CancellationToken cancellationToken = default) =>
        service.ComparePeriodsAsync(
            Context(),
            new McpPeriodsCompareInput(fromA, toA, fromB, toB, metrics),
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
            userId, connectionId, correlationId, ClientId: clientId);
    }
}
