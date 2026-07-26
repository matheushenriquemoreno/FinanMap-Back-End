#nullable enable

using System.Globalization;
using System.Text.Json;
using Application.Mcp.Configuration;
using Application.Mcp.Interfaces;
using Application.Mcp.Models;
using Domain.Enum;
using Domain.Enums;
using Domain.Mcp.Entities;
using Domain.Mcp.Enums;
using Domain.Mcp.Repositories;

namespace Application.Mcp.Services;

public sealed class McpWriteService
{
    private static readonly TimeSpan PreviewValidity = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan PayloadRetention = TimeSpan.FromHours(24);
    private static readonly TimeSpan ExecutionLease = TimeSpan.FromMinutes(1);
    private readonly IMcpWriteDomainGateway _domain;
    private readonly IMcpConnectionValidator _connections;
    private readonly IMcpPreviewRepository _previews;
    private readonly IMcpOperationJournalRepository _audit;
    private readonly IMcpConfirmationJournalRepository _operations;
    private readonly IMcpPreviewPayloadProtector _protector;
    private readonly TimeProvider _time;
    private readonly McpAuditSanitizer _sanitizer;

    public McpWriteService(
        IMcpWriteDomainGateway domain,
        IMcpConnectionValidator connections,
        IMcpPreviewRepository previews,
        IMcpOperationJournalRepository audit,
        IMcpConfirmationJournalRepository operations,
        IMcpPreviewPayloadProtector protector,
        TimeProvider time,
        McpAuditSanitizer sanitizer)
    {
        _domain = domain;
        _connections = connections;
        _previews = previews;
        _audit = audit;
        _operations = operations;
        _protector = protector;
        _time = time;
        _sanitizer = sanitizer;
    }

    public Task<McpToolEnvelope<McpPreviewData>> PrepareCategoryCreateAsync(
        McpCallContext context,
        McpCategoryCreatePreviewInput input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            return ClarificationWithAuditAsync(
                context,
                "finanmap_category_create_preview",
                "name",
                "Informe o nome da categoria.",
                cancellationToken);
        if (!Enum.IsDefined(input.Type))
            return ClarificationWithAuditAsync(
                context,
                "finanmap_category_create_preview",
                "type",
                "Informe um tipo de categoria válido.",
                cancellationToken);
        return PrepareAsync(
            context,
            "finanmap_category_create_preview",
            input.RequestId,
            new McpWriteCommand(
                McpWriteEntity.Category,
                McpPreviewAction.Create,
                null,
                new Dictionary<string, object?>
                {
                    ["name"] = NormalizeText(input.Name),
                    ["type"] = input.Type.ToString()
                }),
            cancellationToken);
    }

    public Task<McpToolEnvelope<McpPreviewData>> PrepareCategoryUpdateAsync(
        McpCallContext context,
        McpCategoryUpdatePreviewInput input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Id))
            return ClarificationWithAuditAsync(
                context,
                "finanmap_category_update_preview",
                "id",
                "Informe a categoria a alterar.",
                cancellationToken);
        if (string.IsNullOrWhiteSpace(input.Name))
            return ClarificationWithAuditAsync(
                context,
                "finanmap_category_update_preview",
                "name",
                "Informe o novo nome da categoria.",
                cancellationToken);
        return PrepareAsync(
            context,
            "finanmap_category_update_preview",
            input.RequestId,
            new McpWriteCommand(
                McpWriteEntity.Category,
                McpPreviewAction.Update,
                input.Id.Trim(),
                new Dictionary<string, object?>
                {
                    ["name"] = NormalizeText(input.Name)
                }),
            cancellationToken);
    }

    public Task<McpToolEnvelope<McpPreviewData>> PrepareCategoryDeleteAsync(
        McpCallContext context,
        McpCategoryDeletePreviewInput input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Id))
            return ClarificationWithAuditAsync(
                context,
                "finanmap_category_delete_preview",
                "id",
                "Informe a categoria a excluir.",
                cancellationToken);
        return PrepareAsync(
            context,
            "finanmap_category_delete_preview",
            input.RequestId,
            new McpWriteCommand(
                McpWriteEntity.Category,
                McpPreviewAction.Delete,
                input.Id.Trim(),
                new Dictionary<string, object?>()),
            cancellationToken);
    }

    public Task<McpToolEnvelope<McpPreviewData>> PrepareIncomeCreateAsync(
        McpCallContext context,
        McpIncomeCreatePreviewInput input,
        CancellationToken cancellationToken = default)
    {
        if (input.Month is < 1 or > 12)
            return ClarificationWithAuditAsync(
                context,
                "finanmap_income_create_preview",
                "month",
                "Informe um mês entre 1 e 12.",
                cancellationToken);
        if (input.Year < _time.GetUtcNow().Year - 5)
            return ClarificationWithAuditAsync(
                context,
                "finanmap_income_create_preview",
                "year",
                "Informe um ano aceito pelo FinanMap.",
                cancellationToken);
        if (string.IsNullOrWhiteSpace(input.Description))
            return ClarificationWithAuditAsync(
                context,
                "finanmap_income_create_preview",
                "description",
                "Informe a descrição da receita.",
                cancellationToken);
        if (!TryMoney(input.Amount, out var amount))
            return ClarificationWithAuditAsync(
                context,
                "finanmap_income_create_preview",
                "amount",
                "Informe um valor positivo com no máximo duas casas decimais.",
                cancellationToken);
        if (string.IsNullOrWhiteSpace(input.CategoryId))
            return ClarificationWithAuditAsync(
                context,
                "finanmap_income_create_preview",
                "categoryId",
                "Informe a categoria da receita.",
                cancellationToken);

        return PrepareAsync(
            context,
            "finanmap_income_create_preview",
            input.RequestId,
            new McpWriteCommand(
                McpWriteEntity.Income,
                McpPreviewAction.Create,
                null,
                new Dictionary<string, object?>
                {
                    ["year"] = input.Year,
                    ["month"] = input.Month,
                    ["description"] = NormalizeText(input.Description),
                    ["amount"] = amount.ToString("0.00", CultureInfo.InvariantCulture),
                    ["categoryId"] = input.CategoryId.Trim()
                }),
            cancellationToken);
    }

    public Task<McpToolEnvelope<McpPreviewData>> PrepareIncomeUpdateAsync(
        McpCallContext context,
        McpIncomeUpdatePreviewInput input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Id))
            return ClarificationWithAuditAsync(
                context,
                "finanmap_income_update_preview",
                "id",
                "Informe a receita a alterar.",
                cancellationToken);
        var values = new Dictionary<string, object?>();
        if (input.Description is not null)
        {
            if (string.IsNullOrWhiteSpace(input.Description))
                return ClarificationWithAuditAsync(
                    context,
                    "finanmap_income_update_preview",
                    "description",
                    "Informe uma descrição válida.",
                    cancellationToken);
            values["description"] = NormalizeText(input.Description);
        }
        if (input.Amount is not null)
        {
            if (!TryMoney(input.Amount, out var amount))
            {
                return ClarificationWithAuditAsync(
                    context,
                    "finanmap_income_update_preview",
                    "amount",
                    "Informe um valor positivo com no máximo duas casas decimais.",
                    cancellationToken);
            }
            values["amount"] = amount.ToString("0.00", CultureInfo.InvariantCulture);
        }
        if (input.CategoryId is not null)
        {
            if (string.IsNullOrWhiteSpace(input.CategoryId))
                return ClarificationWithAuditAsync(
                    context,
                    "finanmap_income_update_preview",
                    "categoryId",
                    "Informe uma categoria válida.",
                    cancellationToken);
            values["categoryId"] = input.CategoryId.Trim();
        }
        if (values.Count == 0)
            return ClarificationWithAuditAsync(
                context,
                "finanmap_income_update_preview",
                "changes",
                "Informe ao menos um campo a alterar.",
                cancellationToken);

        return PrepareAsync(
            context,
            "finanmap_income_update_preview",
            input.RequestId,
            new McpWriteCommand(
                McpWriteEntity.Income,
                McpPreviewAction.Update,
                input.Id.Trim(),
                values),
            cancellationToken);
    }

    public Task<McpToolEnvelope<McpPreviewData>> PrepareIncomeDeleteAsync(
        McpCallContext context,
        McpIncomeDeletePreviewInput input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Id))
            return ClarificationWithAuditAsync(
                context,
                "finanmap_income_delete_preview",
                "id",
                "Informe a receita a excluir.",
                cancellationToken);
        return PrepareAsync(
            context,
            "finanmap_income_delete_preview",
            input.RequestId,
            new McpWriteCommand(
                McpWriteEntity.Income,
                McpPreviewAction.Delete,
                input.Id.Trim(),
                new Dictionary<string, object?>()),
            cancellationToken);
    }

    public Task<McpToolEnvelope<McpPreviewData>> PrepareExpenseCreateAsync(
        McpCallContext context,
        McpExpenseCreatePreviewInput input,
        CancellationToken cancellationToken = default)
    {
        const string toolName = "finanmap_expense_create_preview";
        var invalid = ValidateTransactionCreate(
            context,
            toolName,
            input.Year,
            input.Month,
            input.Description,
            input.Amount,
            input.CategoryId,
            cancellationToken,
            out var amount);
        if (invalid is not null)
            return invalid;
        if (input.IsInstallment && input.IsRecurring)
        {
            return ClarificationWithAuditAsync(
                context,
                toolName,
                "recurrence",
                "Escolha parcelamento ou recorrência; as duas opções não podem ser combinadas.",
                cancellationToken);
        }
        if ((input.IsInstallment || input.IsRecurring) &&
            input.RecurrenceCount is not (>= 2 and <= 24))
        {
            return ClarificationWithAuditAsync(
                context,
                toolName,
                "recurrenceCount",
                "Informe entre 2 e 24 meses para o parcelamento ou recorrência.",
                cancellationToken);
        }
        if (!input.IsInstallment &&
            !input.IsRecurring &&
            input.RecurrenceCount is > 1)
        {
            return ClarificationWithAuditAsync(
                context,
                toolName,
                "recurrence",
                "Indique se a despesa é parcelada ou recorrente.",
                cancellationToken);
        }

        return PrepareAsync(
            context,
            toolName,
            input.RequestId,
            new McpWriteCommand(
                McpWriteEntity.Expense,
                McpPreviewAction.Create,
                null,
                new Dictionary<string, object?>
                {
                    ["year"] = input.Year,
                    ["month"] = input.Month,
                    ["description"] = NormalizeText(input.Description),
                    ["amount"] = amount.ToString("0.00", CultureInfo.InvariantCulture),
                    ["categoryId"] = input.CategoryId.Trim(),
                    ["isInstallment"] = input.IsInstallment,
                    ["isRecurring"] = input.IsRecurring,
                    ["recurrenceCount"] = input.IsInstallment || input.IsRecurring
                        ? input.RecurrenceCount
                        : 1,
                    ["groupingExpenseId"] = NormalizeOptionalId(input.GroupingExpenseId)
                }),
            cancellationToken);
    }

    public Task<McpToolEnvelope<McpPreviewData>> PrepareExpenseUpdateAsync(
        McpCallContext context,
        McpExpenseUpdatePreviewInput input,
        CancellationToken cancellationToken = default)
    {
        const string toolName = "finanmap_expense_update_preview";
        if (string.IsNullOrWhiteSpace(input.Id))
            return ClarificationWithAuditAsync(
                context,
                toolName,
                "id",
                "Informe a despesa a alterar.",
                cancellationToken);
        var values = BuildTransactionChanges(
            context,
            toolName,
            input.Description,
            input.Amount,
            input.CategoryId,
            cancellationToken,
            out var invalid);
        if (invalid is not null)
            return invalid;
        if (input.GroupingExpenseId is not null)
            values["groupingExpenseId"] = NormalizeOptionalId(input.GroupingExpenseId);
        if (input.BatchModifier.HasValue)
            values["batchModifier"] = input.BatchModifier.Value.ToString();
        if (values.Count == 0)
            return ClarificationWithAuditAsync(
                context,
                toolName,
                "changes",
                "Informe ao menos um campo a alterar.",
                cancellationToken);
        return PrepareAsync(
            context,
            toolName,
            input.RequestId,
            new McpWriteCommand(
                McpWriteEntity.Expense,
                McpPreviewAction.Update,
                input.Id.Trim(),
                values),
            cancellationToken);
    }

    public Task<McpToolEnvelope<McpPreviewData>> PrepareExpenseDeleteAsync(
        McpCallContext context,
        McpExpenseDeletePreviewInput input,
        CancellationToken cancellationToken = default)
    {
        const string toolName = "finanmap_expense_delete_preview";
        if (string.IsNullOrWhiteSpace(input.Id))
            return ClarificationWithAuditAsync(
                context,
                toolName,
                "id",
                "Informe a despesa a excluir.",
                cancellationToken);
        var values = new Dictionary<string, object?>();
        if (input.BatchModifier.HasValue)
            values["batchModifier"] = input.BatchModifier.Value.ToString();
        return PrepareAsync(
            context,
            toolName,
            input.RequestId,
            new McpWriteCommand(
                McpWriteEntity.Expense,
                McpPreviewAction.Delete,
                input.Id.Trim(),
                values),
            cancellationToken);
    }

    public Task<McpToolEnvelope<McpPreviewData>> PrepareInvestmentCreateAsync(
        McpCallContext context,
        McpInvestmentCreatePreviewInput input,
        CancellationToken cancellationToken = default)
    {
        const string toolName = "finanmap_investment_create_preview";
        var invalid = ValidateTransactionCreate(
            context,
            toolName,
            input.Year,
            input.Month,
            input.Description,
            input.Amount,
            input.CategoryId,
            cancellationToken,
            out var amount);
        if (invalid is not null)
            return invalid;
        return PrepareAsync(
            context,
            toolName,
            input.RequestId,
            new McpWriteCommand(
                McpWriteEntity.Investment,
                McpPreviewAction.Create,
                null,
                new Dictionary<string, object?>
                {
                    ["year"] = input.Year,
                    ["month"] = input.Month,
                    ["description"] = NormalizeText(input.Description),
                    ["amount"] = amount.ToString("0.00", CultureInfo.InvariantCulture),
                    ["categoryId"] = input.CategoryId.Trim()
                }),
            cancellationToken);
    }

    public Task<McpToolEnvelope<McpPreviewData>> PrepareInvestmentUpdateAsync(
        McpCallContext context,
        McpInvestmentUpdatePreviewInput input,
        CancellationToken cancellationToken = default)
    {
        const string toolName = "finanmap_investment_update_preview";
        if (string.IsNullOrWhiteSpace(input.Id))
            return ClarificationWithAuditAsync(
                context,
                toolName,
                "id",
                "Informe o investimento a alterar.",
                cancellationToken);
        var values = BuildTransactionChanges(
            context,
            toolName,
            input.Description,
            input.Amount,
            input.CategoryId,
            cancellationToken,
            out var invalid);
        if (invalid is not null)
            return invalid;
        if (values.Count == 0)
            return ClarificationWithAuditAsync(
                context,
                toolName,
                "changes",
                "Informe ao menos um campo a alterar.",
                cancellationToken);
        return PrepareAsync(
            context,
            toolName,
            input.RequestId,
            new McpWriteCommand(
                McpWriteEntity.Investment,
                McpPreviewAction.Update,
                input.Id.Trim(),
                values),
            cancellationToken);
    }

    public Task<McpToolEnvelope<McpPreviewData>> PrepareInvestmentDeleteAsync(
        McpCallContext context,
        McpInvestmentDeletePreviewInput input,
        CancellationToken cancellationToken = default)
    {
        const string toolName = "finanmap_investment_delete_preview";
        if (string.IsNullOrWhiteSpace(input.Id))
            return ClarificationWithAuditAsync(
                context,
                toolName,
                "id",
                "Informe o investimento a excluir.",
                cancellationToken);
        return PrepareAsync(
            context,
            toolName,
            input.RequestId,
            new McpWriteCommand(
                McpWriteEntity.Investment,
                McpPreviewAction.Delete,
                input.Id.Trim(),
                new Dictionary<string, object?>()),
            cancellationToken);
    }

    public Task<McpToolEnvelope<McpPreviewData>> PrepareFixedCostCreateAsync(
        McpCallContext context,
        McpFixedCostCreatePreviewInput input,
        CancellationToken cancellationToken = default)
    {
        const string toolName = "finanmap_fixed_cost_create_preview";
        if (string.IsNullOrWhiteSpace(input.Name))
            return ClarificationWithAuditAsync(
                context,
                toolName,
                "name",
                "Informe o nome do custo fixo.",
                cancellationToken);
        if (input.DueDay is < 1 or > 31)
            return ClarificationWithAuditAsync(
                context,
                toolName,
                "dueDay",
                "Informe um dia de vencimento entre 1 e 31.",
                cancellationToken);
        return PrepareAsync(
            context,
            toolName,
            input.RequestId,
            new McpWriteCommand(
                McpWriteEntity.FixedCost,
                McpPreviewAction.Create,
                null,
                new Dictionary<string, object?>
                {
                    ["name"] = NormalizeText(input.Name),
                    ["dueDay"] = input.DueDay,
                    ["categoryId"] = NormalizeOptionalId(input.CategoryId),
                    ["active"] = true
                }),
            cancellationToken);
    }

    public Task<McpToolEnvelope<McpPreviewData>> PrepareFixedCostUpdateAsync(
        McpCallContext context,
        McpFixedCostUpdatePreviewInput input,
        CancellationToken cancellationToken = default)
    {
        const string toolName = "finanmap_fixed_cost_update_preview";
        if (string.IsNullOrWhiteSpace(input.Id))
            return ClarificationWithAuditAsync(
                context,
                toolName,
                "id",
                "Informe o custo fixo a alterar.",
                cancellationToken);
        var values = new Dictionary<string, object?>();
        if (input.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(input.Name))
                return ClarificationWithAuditAsync(
                    context,
                    toolName,
                    "name",
                    "Informe um nome válido para o custo fixo.",
                    cancellationToken);
            values["name"] = NormalizeText(input.Name);
        }
        if (input.DueDay.HasValue)
        {
            if (input.DueDay is < 1 or > 31)
                return ClarificationWithAuditAsync(
                    context,
                    toolName,
                    "dueDay",
                    "Informe um dia de vencimento entre 1 e 31.",
                    cancellationToken);
            values["dueDay"] = input.DueDay.Value;
        }
        if (input.CategoryId is not null)
            values["categoryId"] = NormalizeOptionalId(input.CategoryId);
        if (input.Active.HasValue)
            values["active"] = input.Active.Value;
        if (values.Count == 0)
            return ClarificationWithAuditAsync(
                context,
                toolName,
                "changes",
                "Informe ao menos um campo a alterar.",
                cancellationToken);
        return PrepareAsync(
            context,
            toolName,
            input.RequestId,
            new McpWriteCommand(
                McpWriteEntity.FixedCost,
                McpPreviewAction.Update,
                input.Id.Trim(),
                values),
            cancellationToken);
    }

    public Task<McpToolEnvelope<McpPreviewData>> PrepareFixedCostDeleteAsync(
        McpCallContext context,
        McpFixedCostDeletePreviewInput input,
        CancellationToken cancellationToken = default)
    {
        const string toolName = "finanmap_fixed_cost_delete_preview";
        if (string.IsNullOrWhiteSpace(input.Id))
            return ClarificationWithAuditAsync(
                context,
                toolName,
                "id",
                "Informe o custo fixo a excluir.",
                cancellationToken);
        return PrepareAsync(
            context,
            toolName,
            input.RequestId,
            new McpWriteCommand(
                McpWriteEntity.FixedCost,
                McpPreviewAction.Delete,
                input.Id.Trim(),
                new Dictionary<string, object?>()),
            cancellationToken);
    }

    public async Task<McpToolEnvelope<McpOperationData>> ConfirmAsync(
        McpCallContext context,
        McpOperationConfirmInput input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.PreviewId))
        {
            return await OperationRejectedWithAuditAsync(
                context,
                "finanmap_operation_confirm",
                "VALIDATION_REQUIRED",
                "Informe a prévia específica que deseja confirmar.",
                "previewId",
                cancellationToken);
        }
        if (string.IsNullOrWhiteSpace(input.PayloadHash))
        {
            return await OperationRejectedWithAuditAsync(
                context,
                "finanmap_operation_confirm",
                "VALIDATION_REQUIRED",
                "Informe o hash exibido na prévia.",
                "payloadHash",
                cancellationToken);
        }

        var preview = await _previews.GetOwnedAsync(
            input.PreviewId.Trim(),
            context.UserId,
            context.ConnectionId,
            cancellationToken);
        if (preview is null)
        {
            return await OperationRejectedWithAuditAsync(
                context,
                "finanmap_operation_confirm",
                "PREVIEW_NOT_FOUND",
                "A prévia não foi encontrada para esta conta e conexão.",
                "previewId",
                cancellationToken);
        }

        if (!TryDecision(input.Decision, out var decision) ||
            decision != preview.RequiredDecision)
        {
            return await OperationRejectedWithAuditAsync(
                context,
                "finanmap_operation_confirm",
                "CONFIRMATION_DECISION_INVALID",
                "A decisão não corresponde exatamente à decisão exigida pela prévia.",
                "decision",
                cancellationToken);
        }
        if (!string.Equals(
                input.PayloadHash,
                preview.PayloadHash,
                StringComparison.Ordinal))
        {
            return await OperationRejectedWithAuditAsync(
                context,
                "finanmap_operation_confirm",
                "CONFIRMATION_HASH_INVALID",
                "O hash informado não corresponde ao conteúdo da prévia.",
                "payloadHash",
                cancellationToken);
        }

        try
        {
            await _connections.ValidateActiveAsync(
                context.ConnectionId,
                context.UserId,
                "mcp:write",
                cancellationToken);
        }
        catch (Exception exception)
            when (exception is McpConnectionNotFoundException or
                McpConnectionInactiveException or
                McpScopeMissingException)
        {
            return await OperationRejectedWithAuditAsync(
                context,
                "finanmap_operation_confirm",
                exception is McpScopeMissingException
                    ? "AUTH_SCOPE_MISSING"
                    : "AUTH_CONNECTION_INVALID",
                "A conexão MCP não está autorizada para escritas.",
                null,
                cancellationToken);
        }

        var existingConfirmation = await _operations.GetByPreviewAsync(
            preview.Id,
            context.UserId,
            context.ConnectionId,
            cancellationToken);
        if (existingConfirmation is not null)
            return FromPersisted(context, existingConfirmation);

        var command = DeserializeCommand(preview);
        var plannedSteps = command.Steps is { Count: > 0 }
            ? command.Steps
            :
            [
                new McpWriteStepPlan(
                    "apply",
                    command.TargetId,
                    command.Values,
                    command.ExpectedValues)
            ];
        var journal = McpOperationJournal.Start(
            context.UserId,
            context.ConnectionId,
            context.CorrelationId,
            "finanmap_operation_confirm",
            McpOperationClass.Confirm,
            _sanitizer.Sanitize(new Dictionary<string, object?>
            {
                ["previewIdPresent"] = true,
                ["decision"] = input.Decision
            }),
            Origin(context),
            idempotencyKey: preview.Id,
            requestHash: McpCursorCodec.CanonicalFingerprint(new
            {
                previewId = preview.Id,
                payloadHash = input.PayloadHash,
                decision = input.Decision
            }),
            previewId: preview.Id,
            targetRefs: preview.SnapshotHashes
                .Select(item => new McpTargetRef(item.EntityType, item.EntityId))
                .ToArray(),
            steps: plannedSteps
                .Select(step => new McpOperationStep(
                    step.Name,
                    McpOperationStepState.Pending,
                    null,
                    null,
                    null))
                .ToArray(),
            startedAtUtc: UtcNow());
        var creation = await _operations.CreateOrGetAsync(journal, cancellationToken);
        if (creation.RequestConflict)
        {
            return OperationRejected(
                context,
                "IDEMPOTENCY_CONFLICT",
                "A confirmação já foi usada com argumentos diferentes.",
                null);
        }
        if (!creation.Created)
            return FromPersisted(context, creation.Journal);

        var reserved = await _previews.TryReserveAsync(
            preview.Id,
            context.UserId,
            context.ConnectionId,
            input.PayloadHash,
            decision,
            journal.Id,
            UtcNow(),
            cancellationToken);
        if (reserved is null)
        {
            journal.Reject(
                UtcNow() >= preview.ExpiresAtUtc
                    ? "PREVIEW_EXPIRED"
                    : "PREVIEW_ALREADY_CONSUMED",
                UtcNow());
            await _operations.ReplaceAsync(journal, 0, cancellationToken);
            return OperationRejected(
                context,
                UtcNow() >= preview.ExpiresAtUtc
                    ? "PREVIEW_EXPIRED"
                    : "PREVIEW_ALREADY_CONSUMED",
                UtcNow() >= preview.ExpiresAtUtc
                    ? "A prévia expirou; prepare uma nova."
                    : "A prévia já foi consumida.",
                "previewId");
        }

        var leaseOwner = $"confirm:{context.CorrelationId}";
        var leased = await _operations.TryAcquireLeaseAsync(
            journal.Id,
            leaseOwner,
            UtcNow(),
            ExecutionLease,
            cancellationToken);
        if (leased is null)
            return FromPersisted(context, journal);

        for (var index = 0; index < plannedSteps.Count; index++)
        {
            var plan = plannedSteps[index];
            var expectedBeforeStart = leased.Version;
            leased.StartStep(plan.Name, leaseOwner, UtcNow());
            if (!await _operations.ReplaceAsync(
                    leased,
                    expectedBeforeStart,
                    cancellationToken))
            {
                return PersistencePending(context, journal.Id);
            }

            var stepCommand = CommandForStep(command, plan);
            var stepMarker = plannedSteps.Count == 1
                ? journal.Id
                : $"{journal.Id}:{plan.Name}";
            McpDomainEffect effect;
            try
            {
                effect = await _domain.ExecuteAsync(
                    context.UserId,
                    stepCommand,
                    stepMarker,
                    preview.SnapshotHashes,
                    cancellationToken);
            }
            catch
            {
                effect = McpDomainEffect.Unknown(
                    "Não foi possível comprovar o resultado da escrita. Consulte o status antes de tentar novamente.");
            }

            var isLast = index == plannedSteps.Count - 1;
            if (effect.State == McpDomainEffectState.Completed && !isLast)
            {
                var expectedBeforeComplete = leased.Version;
                leased.EnsureTargetRef(
                    EntityWire(command.Entity),
                    effect.EntityId!);
                leased.CompleteStep(
                    plan.Name,
                    effect.EffectMarker ?? stepMarker,
                    effect.Result,
                    UtcNow());
                if (!await _operations.ReplaceAsync(
                        leased,
                        expectedBeforeComplete,
                        cancellationToken))
                {
                    return PersistencePending(context, journal.Id);
                }
                continue;
            }

            return await PersistEffectAsync(
                context,
                reserved,
                leased,
                leased.Version,
                command,
                plan.Name,
                effect,
                cancellationToken);
        }

        throw new InvalidOperationException("Operação MCP sem passo executável.");
    }

    public async Task<McpToolEnvelope<McpOperationData>> CancelAsync(
        McpCallContext context,
        McpOperationCancelInput input,
        CancellationToken cancellationToken = default)
    {
        var invocation = await StartOperationInvocationAuditAsync(
            context,
            "finanmap_operation_cancel",
            McpOperationClass.Confirm,
            "cancel",
            !string.IsNullOrWhiteSpace(input.PreviewId),
            cancellationToken);
        if (string.IsNullOrWhiteSpace(input.PreviewId))
        {
            return await RejectOperationInvocationAsync(
                context,
                invocation,
                "VALIDATION_REQUIRED",
                "Informe a prévia específica que deseja cancelar.",
                "previewId",
                cancellationToken);
        }

        try
        {
            await _connections.ValidateActiveAsync(
                context.ConnectionId,
                context.UserId,
                "mcp:write",
                cancellationToken);
        }
        catch (Exception exception)
            when (exception is McpConnectionNotFoundException or
                McpConnectionInactiveException or
                McpScopeMissingException)
        {
            return await RejectOperationInvocationAsync(
                context,
                invocation,
                exception is McpScopeMissingException
                    ? "AUTH_SCOPE_MISSING"
                    : "AUTH_CONNECTION_INVALID",
                "A conexão MCP não está autorizada para escritas.",
                null,
                cancellationToken);
        }

        var preview = await _previews.GetOwnedAsync(
            input.PreviewId.Trim(),
            context.UserId,
            context.ConnectionId,
            cancellationToken);
        if (preview is null)
        {
            return await RejectOperationInvocationAsync(
                context,
                invocation,
                "PREVIEW_NOT_FOUND",
                "A prévia não foi encontrada para esta conta e conexão.",
                "previewId",
                cancellationToken);
        }
        if (preview.State != McpPreviewState.Prepared)
        {
            return await RejectOperationInvocationAsync(
                context,
                invocation,
                preview.State == McpPreviewState.Expired
                    ? "PREVIEW_EXPIRED"
                    : "PREVIEW_ALREADY_CONSUMED",
                preview.State == McpPreviewState.Expired
                    ? "A prévia expirou; prepare uma nova."
                    : "A prévia já foi consumida.",
                "previewId",
                cancellationToken);
        }

        var cancelled = await _previews.TryCancelAsync(
            preview.Id,
            context.UserId,
            context.ConnectionId,
            UtcNow(),
            cancellationToken);
        if (cancelled is null || cancelled.State != McpPreviewState.Cancelled)
        {
            return await RejectOperationInvocationAsync(
                context,
                invocation,
                UtcNow() >= preview.ExpiresAtUtc
                    ? "PREVIEW_EXPIRED"
                    : "PREVIEW_ALREADY_CONSUMED",
                UtcNow() >= preview.ExpiresAtUtc
                    ? "A prévia expirou; prepare uma nova."
                    : "A prévia já foi consumida.",
                "previewId",
                cancellationToken);
        }

        invocation.CompleteAt(
            new Dictionary<string, object?>
            {
                ["action"] = "cancel",
                ["operationState"] = "cancelled",
                ["summary"] = "Prévia cancelada sem executar alterações.",
                ["preview"] = PreviewAuditSummary(cancelled)
            },
            UtcNow());
        await _audit.CompleteAsync(
            invocation,
            invocation.ResultSummary,
            cancellationToken);
        return FromPersisted(context, invocation);
    }

    public async Task<McpToolEnvelope<McpOperationData>> GetStatusAsync(
        McpCallContext context,
        McpOperationStatusInput input,
        CancellationToken cancellationToken = default)
    {
        var invocation = await StartOperationInvocationAuditAsync(
            context,
            "finanmap_operation_status",
            McpOperationClass.Read,
            "status",
            !string.IsNullOrWhiteSpace(input.OperationId),
            cancellationToken);
        if (string.IsNullOrWhiteSpace(input.OperationId))
        {
            return await RejectOperationInvocationAsync(
                context,
                invocation,
                "VALIDATION_REQUIRED",
                "Informe o operationId que deseja consultar.",
                "operationId",
                cancellationToken);
        }

        try
        {
            await _connections.ValidateActiveAsync(
                context.ConnectionId,
                context.UserId,
                "mcp:write",
                cancellationToken);
        }
        catch (Exception exception)
            when (exception is McpConnectionNotFoundException or
                McpConnectionInactiveException or
                McpScopeMissingException)
        {
            return await RejectOperationInvocationAsync(
                context,
                invocation,
                exception is McpScopeMissingException
                    ? "AUTH_SCOPE_MISSING"
                    : "AUTH_CONNECTION_INVALID",
                "A conexão MCP não está autorizada para escritas.",
                null,
                cancellationToken);
        }

        var journal = await _operations.GetOwnedAsync(
            input.OperationId.Trim(),
            context.UserId,
            cancellationToken);
        if (journal is null)
        {
            return await RejectOperationInvocationAsync(
                context,
                invocation,
                "OPERATION_NOT_FOUND",
                "A operação não foi encontrada para esta conta.",
                "operationId",
                cancellationToken);
        }

        invocation.CompleteAt(
            new Dictionary<string, object?>
            {
                ["action"] = "status",
                ["operationState"] = journal.State.ToString().ToLowerInvariant(),
                ["summary"] = "Status da operação consultado com sucesso."
            },
            UtcNow());
        await _audit.CompleteAsync(
            invocation,
            invocation.ResultSummary,
            cancellationToken);
        return FromPersisted(context, journal);
    }

    private async Task<McpToolEnvelope<McpPreviewData>> PrepareAsync(
        McpCallContext context,
        string toolName,
        string requestId,
        McpWriteCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            return await ClarificationWithAuditAsync(
                context,
                toolName,
                "requestId",
                "Informe um requestId único para esta preparação.",
                cancellationToken);
        if (requestId.Trim().Length > 100)
            return await ClarificationWithAuditAsync(
                context,
                toolName,
                "requestId",
                "O requestId deve ter no máximo 100 caracteres.",
                cancellationToken);

        var journal = McpOperationJournal.Start(
            context.UserId,
            context.ConnectionId,
            context.CorrelationId,
            toolName,
            McpOperationClass.Preview,
            _sanitizer.Sanitize(new Dictionary<string, object?>
            {
                ["entityType"] = EntityWire(command.Entity),
                ["action"] = command.Action.ToString().ToLowerInvariant(),
                ["requestIdPresent"] = true,
                ["targetPresent"] = command.TargetId is not null,
                ["irreversible"] = command.Action == McpPreviewAction.Delete
            }),
            Origin(context),
            idempotencyKey: requestId.Trim(),
            requestHash: McpCursorCodec.CanonicalFingerprint(command));
        McpJournalCreateResult journalCreation;
        try
        {
            journalCreation = await _operations.CreateOrGetAsync(
                journal,
                cancellationToken);
        }
        catch (Exception exception)
        {
            throw new McpJournalUnavailableException(exception);
        }
        if (journalCreation.RequestConflict)
        {
            return Envelope(
                context,
                "rejected",
                null,
                [
                    new McpToolError(
                        "IDEMPOTENCY_CONFLICT",
                        "O requestId já foi usado com dados diferentes.",
                        "requestId",
                        false)
                ]);
        }
        if (!journalCreation.Created)
        {
            var replayPreview = await _previews.GetByRequestAsync(
                context.UserId,
                context.ConnectionId,
                toolName,
                requestId.Trim(),
                cancellationToken);
            if (replayPreview is null ||
                replayPreview.PayloadCiphertext.Length == 0)
            {
                return Envelope(
                    context,
                    "unknown",
                    null,
                    [
                        new McpToolError(
                            "PREVIEW_REPLAY_UNAVAILABLE",
                            "A preparação existe, mas sua prévia não pôde ser recuperada. Consulte o histórico antes de tentar novamente.",
                            null,
                            false)
                    ]);
            }

            var replayCommand = DeserializeCommand(replayPreview);
            var replayPreparation = PreparationFromProtectedCommand(
                replayCommand,
                replayPreview.SnapshotHashes);
            return Envelope(
                context,
                "requires_confirmation",
                MapPreview(replayPreview, replayPreparation),
                []);
        }

        try
        {
            await _connections.ValidateActiveAsync(
                context.ConnectionId,
                context.UserId,
                "mcp:write",
                cancellationToken);
            var preparation = await _domain.PrepareAsync(
                context.UserId,
                command,
                cancellationToken);
            if (!preparation.IsReady)
            {
                journal.SetResultSummary(new Dictionary<string, object?>
                {
                    ["status"] = "needs_clarification",
                    ["summary"] = "A prévia foi rejeitada pelas regras do FinanMap.",
                    ["errorCodes"] = preparation.Errors.Select(item => item.Code).ToArray()
                });
                journal.Reject(
                    preparation.Errors.FirstOrDefault()?.Code ?? "DOMAIN_REJECTED",
                    UtcNow());
                await _audit.CompleteAsync(
                    journal,
                    journal.ResultSummary,
                    cancellationToken);
                return Envelope(
                    context,
                    "needs_clarification",
                    null,
                    preparation.Errors);
            }

            var normalized = preparation.Command;
            var serialized = JsonSerializer.SerializeToUtf8Bytes(normalized);
            var payloadHash = McpCursorCodec.CanonicalFingerprint(normalized);
            var now = UtcNow();
            var safeSummary = BuildSafeSummary(preparation);
            var preview = McpPreview.Prepare(
                context.UserId,
                context.ConnectionId,
                toolName,
                normalized.Action,
                requestId.Trim(),
                _protector.Protect(serialized),
                payloadHash,
                preparation.Snapshots,
                safeSummary,
                RequiredDecision(normalized.Action),
                now,
                PreviewValidity,
                PayloadRetention);
            var persisted = await _previews.CreateOrGetAsync(preview, cancellationToken);
            if (persisted.PayloadConflict)
            {
                journal.Reject("IDEMPOTENCY_CONFLICT", UtcNow());
                await _audit.FailAsync(
                    journal,
                    "IDEMPOTENCY_CONFLICT",
                    cancellationToken);
                return Envelope(
                    context,
                    "rejected",
                    null,
                    [
                        new McpToolError(
                            "IDEMPOTENCY_CONFLICT",
                            "O requestId já foi usado com dados diferentes.",
                            "requestId",
                            false)
                    ]);
            }

            var data = MapPreview(persisted.Preview, preparation);
            journal.Complete(new Dictionary<string, object?>
            {
                ["status"] = "requires_confirmation",
                ["previewId"] = persisted.Preview.Id,
                ["action"] = normalized.Action.ToString().ToLowerInvariant(),
                ["targetCount"] = data.Targets.Count,
                ["irreversible"] = data.Irreversible,
                ["summary"] = "Prévia preparada; nenhuma alteração financeira foi executada.",
                ["preview"] = PreviewAuditSummary(persisted.Preview)
            });
            await _audit.CompleteAsync(
                journal,
                journal.ResultSummary,
                cancellationToken);
            return Envelope(context, "requires_confirmation", data, []);
        }
        catch (McpScopeMissingException)
        {
            journal.Fail("AUTH_SCOPE_MISSING");
            await _audit.FailAsync(
                journal,
                "AUTH_SCOPE_MISSING",
                cancellationToken);
            return Envelope(
                context,
                "rejected",
                null,
                [
                    new McpToolError(
                        "AUTH_SCOPE_MISSING",
                        "A conexão MCP não possui o escopo mcp:write.",
                        null,
                        false)
                ]);
        }
        catch (McpConnectionNotFoundException)
        {
            journal.Fail("AUTH_CONNECTION_INVALID");
            await _audit.FailAsync(
                journal,
                "AUTH_CONNECTION_INVALID",
                cancellationToken);
            return Envelope(
                context,
                "rejected",
                null,
                [
                    new McpToolError(
                        "AUTH_CONNECTION_INVALID",
                        "A conexão MCP não é válida para esta conta.",
                        null,
                        false)
                ]);
        }
    }

    private async Task<McpToolEnvelope<McpOperationData>> PersistEffectAsync(
        McpCallContext context,
        McpPreview preview,
        McpOperationJournal journal,
        int journalExpectedVersion,
        McpWriteCommand command,
        string stepName,
        McpDomainEffect effect,
        CancellationToken cancellationToken)
    {
        var previewExpectedVersion = preview.Version;
        switch (effect.State)
        {
            case McpDomainEffectState.Completed:
                journal.EnsureTargetRef(
                    EntityWire(command.Entity),
                    effect.EntityId!);
                journal.CompleteStep(
                    stepName,
                    effect.EffectMarker ?? journal.Id,
                    effect.Result,
                    UtcNow());
                journal.CompleteAt(
                    new Dictionary<string, object?>
                    {
                        ["status"] = "success",
                        ["entityType"] = EntityWire(command.Entity),
                        ["entityId"] = effect.EntityId,
                        ["action"] = command.Action.ToString().ToLowerInvariant(),
                        ["summary"] = SafeOperationSummary(command),
                        ["preview"] = PreviewAuditSummary(preview),
                        ["confirmation"] = ConfirmationAuditSummary(preview),
                        ["reconciliation"] = new Dictionary<string, object?>
                        {
                            ["status"] = "not_required",
                            ["summary"] = "O efeito foi comprovado durante a confirmação."
                        }
                    },
                    UtcNow());
                if (!await _operations.ReplaceAsync(
                        journal,
                        journalExpectedVersion,
                        cancellationToken))
                {
                    return PersistencePending(context, journal.Id);
                }
                preview.Complete();
                await _previews.ReplaceAsync(
                    preview,
                    previewExpectedVersion,
                    cancellationToken);
                return FromPersisted(context, journal);

            case McpDomainEffectState.ConflictChanged:
            case McpDomainEffectState.Rejected:
                var knownCode = effect.ErrorCode ?? "DOMAIN_REJECTED";
                journal.FailStep(stepName, knownCode, false, UtcNow());
                journal.SetResultSummary(new Dictionary<string, object?>
                {
                    ["action"] = command.Action.ToString().ToLowerInvariant(),
                    ["entityType"] = EntityWire(command.Entity),
                    ["entityId"] = command.TargetId,
                    ["summary"] = "A escrita confirmada foi rejeitada sem efeito pendente.",
                    ["preview"] = PreviewAuditSummary(preview),
                    ["confirmation"] = ConfirmationAuditSummary(preview),
                    ["failure"] = new Dictionary<string, object?>
                    {
                        ["code"] = knownCode,
                        ["message"] = effect.Message ??
                            "A operação foi rejeitada pelas regras do FinanMap.",
                        ["guidance"] = knownCode == "CONFLICT_CHANGED"
                            ? "Prepare uma nova prévia com os dados atuais."
                            : "Corrija o impedimento informado antes de preparar outra prévia."
                    }
                });
                journal.FailAt(knownCode, UtcNow());
                if (!await _operations.ReplaceAsync(
                        journal,
                        journalExpectedVersion,
                        cancellationToken))
                {
                    return PersistencePending(context, journal.Id);
                }
                preview.Fail(false);
                await _previews.ReplaceAsync(
                    preview,
                    previewExpectedVersion,
                    cancellationToken);
                return OperationRejected(
                    context,
                    knownCode,
                    effect.Message ?? "A operação foi rejeitada pelas regras do FinanMap.",
                    null);

            default:
                journal.FailStep(
                    stepName,
                    "EFFECT_OUTCOME_UNKNOWN",
                    true,
                    UtcNow());
                journal.SetResultSummary(new Dictionary<string, object?>
                {
                    ["action"] = command.Action.ToString().ToLowerInvariant(),
                    ["entityType"] = EntityWire(command.Entity),
                    ["entityId"] = command.TargetId,
                    ["summary"] = "O resultado da escrita ainda não pôde ser comprovado.",
                    ["preview"] = PreviewAuditSummary(preview),
                    ["confirmation"] = ConfirmationAuditSummary(preview),
                    ["reconciliation"] = new Dictionary<string, object?>
                    {
                        ["status"] = "unknown",
                        ["summary"] = "O efeito não foi comprovado na janela de confirmação.",
                        ["guidance"] = "Consulte o operationId; não repita a escrita."
                    },
                    ["failure"] = new Dictionary<string, object?>
                    {
                        ["code"] = "EFFECT_OUTCOME_UNKNOWN",
                        ["message"] = effect.Message ??
                            "O resultado da escrita é desconhecido.",
                        ["guidance"] = "Consulte o status antes de qualquer nova tentativa."
                    }
                });
                journal.MarkUnknown("EFFECT_OUTCOME_UNKNOWN", UtcNow());
                if (!await _operations.ReplaceAsync(
                        journal,
                        journalExpectedVersion,
                        cancellationToken))
                {
                    return PersistencePending(context, journal.Id);
                }
                preview.Fail(true);
                await _previews.ReplaceAsync(
                    preview,
                    previewExpectedVersion,
                    cancellationToken);
                return FromPersisted(context, journal);
        }
    }

    private McpWriteCommand DeserializeCommand(McpPreview preview)
    {
        var plaintext = _protector.Unprotect(preview.PayloadCiphertext);
        return JsonSerializer.Deserialize<McpWriteCommand>(plaintext)
               ?? throw new InvalidOperationException("Payload da prévia MCP inválido.");
    }

    private static McpWriteCommand CommandForStep(
        McpWriteCommand command,
        McpWriteStepPlan step) =>
        command with
        {
            TargetId = step.TargetId,
            Values = new Dictionary<string, object?>(step.Values),
            ExpectedValues = step.ExpectedValues is null
                ? null
                : new Dictionary<string, object?>(step.ExpectedValues),
            Steps = null,
            StepType = step.Type
        };

    private static McpDomainPreparation PreparationFromProtectedCommand(
        McpWriteCommand command,
        IReadOnlyList<McpSnapshotHash> snapshots)
    {
        var current = command.ExpectedValues;
        IReadOnlyDictionary<string, object?> proposed;
        if (command.Action == McpPreviewAction.Delete)
        {
            proposed = new Dictionary<string, object?>();
        }
        else if (current is null)
        {
            proposed = new Dictionary<string, object?>(command.Values);
        }
        else
        {
            var merged = new Dictionary<string, object?>(current);
            foreach (var change in command.Values)
                merged[change.Key] = change.Value;
            proposed = merged;
        }

        return McpDomainPreparation.Ready(
            command,
            current,
            proposed,
            snapshots);
    }

    private static McpToolEnvelope<McpOperationData> FromPersisted(
        McpCallContext context,
        McpOperationJournal journal)
    {
        var state = journal.State switch
        {
            McpOperationState.Completed => "success",
            McpOperationState.Unknown => "unknown",
            McpOperationState.Failed or McpOperationState.Rejected => "rejected",
            _ => "processing"
        };
        journal.ResultSummary.TryGetValue("entityType", out var entityType);
        journal.ResultSummary.TryGetValue("entityId", out var entityId);
        journal.ResultSummary.TryGetValue("summary", out var summary);
        journal.ResultSummary.TryGetValue("operationState", out var operationState);
        var data = new McpOperationData(
            journal.Id,
            operationState?.ToString() ?? journal.State.ToString().ToLowerInvariant(),
            entityType?.ToString(),
            entityId?.ToString(),
            summary?.ToString() ?? (state == "unknown"
                ? "Resultado desconhecido; consulte este operationId antes de tentar novamente."
                : "Operação em processamento."),
            false,
            journal.ResultSummary);
        var errors = state == "unknown"
            ?
            [
                new McpToolError(
                    "RESULT_UNKNOWN",
                    "O resultado da escrita ainda não pôde ser comprovado. Consulte o status; não repita a escrita.",
                    null,
                    false)
            ]
            : Array.Empty<McpToolError>();
        return new McpToolEnvelope<McpOperationData>(
            McpProtocolContract.SchemaVersion,
            context.CorrelationId,
            state,
            data,
            null,
            null,
            new Dictionary<string, object?>(),
            null,
            [],
            errors);
    }

    private static McpPreviewData MapPreview(
        McpPreview preview,
        McpDomainPreparation preparation)
    {
        var targets = preview.SnapshotHashes.Count > 0
            ? preview.SnapshotHashes
                .Select(item => new McpPreviewTarget(item.EntityType, item.EntityId))
                .ToArray()
            :
            [
                new McpPreviewTarget(
                    EntityWire(preparation.Command.Entity),
                    "new")
            ];
        return new McpPreviewData(
            preview.Id,
            preparation.Command.Action.ToString().ToLowerInvariant(),
            targets,
            preparation.CurrentValues,
            preparation.ProposedValues,
            BuildChanges(preparation),
            preparation.Command.Action == McpPreviewAction.Delete,
            DecisionWire(preview.RequiredDecision),
            preview.ExpiresAtUtc,
            preview.PayloadHash);
    }

    private static IReadOnlyDictionary<string, object?> BuildSafeSummary(
        McpDomainPreparation preparation) =>
        new Dictionary<string, object?>
        {
            ["resourceType"] = EntityWire(preparation.Command.Entity),
            ["recordReference"] = preparation.Command.TargetId ?? "new",
            ["action"] = preparation.Command.Action.ToString().ToLowerInvariant(),
            ["changes"] = BuildChanges(preparation)
                .Select(item => new Dictionary<string, object?>
                {
                    ["field"] = item.Field,
                    ["label"] = item.Label,
                    ["currentValue"] = item.CurrentValue,
                    ["proposedValue"] = item.ProposedValue
                })
                .ToArray(),
            ["irreversible"] = preparation.Command.Action == McpPreviewAction.Delete,
            ["requiredDecision"] = DecisionWire(
                RequiredDecision(preparation.Command.Action))
        };

    private static IReadOnlyDictionary<string, object?> PreviewAuditSummary(
        McpPreview preview)
    {
        var summary = new Dictionary<string, object?>(preview.SafeSummary)
        {
            ["expiresAtUtc"] = preview.ExpiresAtUtc
        };
        return summary;
    }

    private static IReadOnlyDictionary<string, object?> ConfirmationAuditSummary(
        McpPreview preview) =>
        new Dictionary<string, object?>
        {
            ["decision"] = DecisionWire(preview.RequiredDecision),
            ["confirmedAtUtc"] = preview.ConsumedAtUtc
        };

    private static IReadOnlyList<McpPreviewChange> BuildChanges(
        McpDomainPreparation preparation)
    {
        var keys = (preparation.CurrentValues?.Keys ?? [])
            .Concat(preparation.ProposedValues.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        return keys.Select(field =>
        {
            object? current = null;
            preparation.CurrentValues?.TryGetValue(field, out current);
            preparation.ProposedValues.TryGetValue(field, out var proposed);
            return new McpPreviewChange(
                field,
                FieldLabel(field),
                current,
                proposed);
        }).ToArray();
    }

    private static string FieldLabel(string field) => field switch
    {
        "name" => "Nome",
        "type" => "Tipo",
        "description" => "Descrição",
        "amount" => "Valor",
        "categoryId" => "Categoria",
        "year" => "Ano",
        "month" => "Mês",
        _ => field
    };

    private async Task<McpToolEnvelope<McpPreviewData>> ClarificationWithAuditAsync(
        McpCallContext context,
        string toolName,
        string field,
        string message,
        CancellationToken cancellationToken)
    {
        var journal = McpOperationJournal.Start(
            context.UserId,
            context.ConnectionId,
            context.CorrelationId,
            toolName,
            McpOperationClass.Preview,
            _sanitizer.Sanitize(new Dictionary<string, object?>
            {
                ["status"] = "needs_clarification"
            }),
            Origin(context),
            startedAtUtc: UtcNow());
        try
        {
            await _audit.AddAsync(journal, cancellationToken);
        }
        catch (Exception exception)
        {
            throw new McpJournalUnavailableException(exception);
        }

        journal.SetResultSummary(new Dictionary<string, object?>
        {
            ["summary"] = "A prévia não foi criada porque faltam dados válidos.",
            ["failure"] = new Dictionary<string, object?>
            {
                ["code"] = "VALIDATION_REQUIRED",
                ["message"] = message,
                ["guidance"] = $"Corrija o campo {field}."
            }
        });
        journal.Reject("VALIDATION_REQUIRED", UtcNow());
        await _audit.CompleteAsync(
            journal,
            journal.ResultSummary,
            cancellationToken);
        return Envelope(
            context,
            "needs_clarification",
            null,
            [
                new McpToolError(
                    "VALIDATION_REQUIRED",
                    message,
                    field,
                    false,
                    new Dictionary<string, object?>
                    {
                        ["guidance"] =
                            $"Corrija o campo {field} e prepare uma nova prévia."
                    })
            ]);
    }

    private static McpToolEnvelope<McpPreviewData> Envelope(
        McpCallContext context,
        string status,
        McpPreviewData? data,
        IReadOnlyList<McpToolError> errors) =>
        new(
            McpProtocolContract.SchemaVersion,
            context.CorrelationId,
            status,
            data,
            null,
            null,
            new Dictionary<string, object?>(),
            null,
            [],
            errors);

    private async Task<McpOperationJournal> StartOperationInvocationAuditAsync(
        McpCallContext context,
        string toolName,
        McpOperationClass operationClass,
        string action,
        bool identifierPresent,
        CancellationToken cancellationToken)
    {
        var journal = McpOperationJournal.Start(
            context.UserId,
            context.ConnectionId,
            context.CorrelationId,
            toolName,
            operationClass,
            _sanitizer.Sanitize(new Dictionary<string, object?>
            {
                ["action"] = action,
                ["identifierPresent"] = identifierPresent
            }),
            Origin(context),
            startedAtUtc: UtcNow());
        try
        {
            await _audit.AddAsync(journal, cancellationToken);
        }
        catch (Exception exception)
        {
            throw new McpJournalUnavailableException(exception);
        }

        return journal;
    }

    private async Task<McpToolEnvelope<McpOperationData>> RejectOperationInvocationAsync(
        McpCallContext context,
        McpOperationJournal journal,
        string code,
        string message,
        string? field,
        CancellationToken cancellationToken)
    {
        journal.SetResultSummary(new Dictionary<string, object?>
        {
            ["action"] = journal.ToolName.EndsWith(
                "_cancel",
                StringComparison.Ordinal)
                ? "cancel"
                : "status",
            ["summary"] = "A operação foi rejeitada antes de qualquer efeito financeiro.",
            ["failure"] = new Dictionary<string, object?>
            {
                ["code"] = code,
                ["message"] = message,
                ["guidance"] = field is null
                    ? "Revise a autorização da conexão."
                    : $"Revise o campo {field}."
            }
        });
        journal.Reject(code, UtcNow());
        await _audit.CompleteAsync(
            journal,
            journal.ResultSummary,
            cancellationToken);
        return OperationRejected(context, code, message, field);
    }

    private async Task<McpToolEnvelope<McpOperationData>> OperationRejectedWithAuditAsync(
        McpCallContext context,
        string toolName,
        string code,
        string message,
        string? field,
        CancellationToken cancellationToken)
    {
        var journal = McpOperationJournal.Start(
            context.UserId,
            context.ConnectionId,
            context.CorrelationId,
            toolName,
            McpOperationClass.Confirm,
            _sanitizer.Sanitize(new Dictionary<string, object?>
            {
                ["status"] = "rejected"
            }),
            Origin(context),
            startedAtUtc: UtcNow());
        try
        {
            await _audit.AddAsync(journal, cancellationToken);
        }
        catch (Exception exception)
        {
            throw new McpJournalUnavailableException(exception);
        }

        journal.SetResultSummary(new Dictionary<string, object?>
        {
            ["summary"] = "A operação foi rejeitada antes de qualquer efeito financeiro.",
            ["failure"] = new Dictionary<string, object?>
            {
                ["code"] = code,
                ["message"] = message,
                ["guidance"] = field is null
                    ? "Revise a autorização da conexão."
                    : $"Revise o campo {field}."
            }
        });
        journal.Reject(code, UtcNow());
        await _audit.CompleteAsync(
            journal,
            journal.ResultSummary,
            cancellationToken);
        return OperationRejected(context, code, message, field);
    }

    private static McpToolEnvelope<McpOperationData> OperationRejected(
        McpCallContext context,
        string code,
        string message,
        string? field) =>
        new(
            McpProtocolContract.SchemaVersion,
            context.CorrelationId,
            "rejected",
            null,
            null,
            null,
            new Dictionary<string, object?>(),
            null,
            [],
            [new McpToolError(code, message, field, false)]);

    private static McpToolEnvelope<McpOperationData> PersistencePending(
        McpCallContext context,
        string operationId) =>
        new(
            McpProtocolContract.SchemaVersion,
            context.CorrelationId,
            "unknown",
            new McpOperationData(
                operationId,
                "reconciling",
                null,
                null,
                "O efeito pode ter ocorrido, mas o resultado durável ainda não foi confirmado.",
                false,
                null),
            null,
            null,
            new Dictionary<string, object?>(),
            null,
            [],
            [
                new McpToolError(
                    "RESULT_PERSISTENCE_PENDING",
                    "Consulte o operationId; não repita a escrita.",
                    null,
                    false)
            ]);

    private Task<McpToolEnvelope<McpPreviewData>>? ValidateTransactionCreate(
        McpCallContext context,
        string toolName,
        int year,
        int month,
        string description,
        string amountText,
        string categoryId,
        CancellationToken cancellationToken,
        out decimal amount)
    {
        amount = 0;
        if (month is < 1 or > 12)
            return ClarificationWithAuditAsync(
                context,
                toolName,
                "month",
                "Informe um mês entre 1 e 12.",
                cancellationToken);
        if (year < _time.GetUtcNow().Year - 5)
            return ClarificationWithAuditAsync(
                context,
                toolName,
                "year",
                "Informe um ano aceito pelo FinanMap.",
                cancellationToken);
        if (string.IsNullOrWhiteSpace(description))
            return ClarificationWithAuditAsync(
                context,
                toolName,
                "description",
                "Informe a descrição do registro financeiro.",
                cancellationToken);
        if (!TryMoney(amountText, out amount))
            return ClarificationWithAuditAsync(
                context,
                toolName,
                "amount",
                "Informe um valor positivo com no máximo duas casas decimais.",
                cancellationToken);
        if (string.IsNullOrWhiteSpace(categoryId))
            return ClarificationWithAuditAsync(
                context,
                toolName,
                "categoryId",
                "Informe a categoria do registro financeiro.",
                cancellationToken);
        return null;
    }

    private Dictionary<string, object?> BuildTransactionChanges(
        McpCallContext context,
        string toolName,
        string? description,
        string? amountText,
        string? categoryId,
        CancellationToken cancellationToken,
        out Task<McpToolEnvelope<McpPreviewData>>? invalid)
    {
        invalid = null;
        var values = new Dictionary<string, object?>();
        if (description is not null)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                invalid = ClarificationWithAuditAsync(
                    context,
                    toolName,
                    "description",
                    "Informe uma descrição válida.",
                    cancellationToken);
                return values;
            }
            values["description"] = NormalizeText(description);
        }
        if (amountText is not null)
        {
            if (!TryMoney(amountText, out var amount))
            {
                invalid = ClarificationWithAuditAsync(
                    context,
                    toolName,
                    "amount",
                    "Informe um valor positivo com no máximo duas casas decimais.",
                    cancellationToken);
                return values;
            }
            values["amount"] = amount.ToString("0.00", CultureInfo.InvariantCulture);
        }
        if (categoryId is not null)
        {
            if (string.IsNullOrWhiteSpace(categoryId))
            {
                invalid = ClarificationWithAuditAsync(
                    context,
                    toolName,
                    "categoryId",
                    "Informe uma categoria válida.",
                    cancellationToken);
                return values;
            }
            values["categoryId"] = categoryId.Trim();
        }
        return values;
    }

    private static bool TryMoney(string value, out decimal amount)
    {
        amount = 0;
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) ||
            !decimal.TryParse(
                normalized,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out amount) ||
            amount <= 0)
        {
            return false;
        }
        var separator = normalized.IndexOf('.');
        return separator < 0 || normalized.Length - separator - 1 <= 2;
    }

    private static string NormalizeText(string value) =>
        value.Trim().Length <= 200 ? value.Trim() : value.Trim()[..200];

    private static string? NormalizeOptionalId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryDecision(
        string value,
        out McpRequiredDecision decision)
    {
        decision = value switch
        {
            "APPLY_CHANGES" => McpRequiredDecision.ApplyChanges,
            "DELETE_PERMANENTLY" => McpRequiredDecision.DeletePermanently,
            "IMPORT_VALID_ITEMS" => McpRequiredDecision.ImportValidItems,
            _ => default
        };
        return value is "APPLY_CHANGES" or
            "DELETE_PERMANENTLY" or
            "IMPORT_VALID_ITEMS";
    }

    private static McpRequiredDecision RequiredDecision(McpPreviewAction action) =>
        action == McpPreviewAction.Delete
            ? McpRequiredDecision.DeletePermanently
            : action == McpPreviewAction.Import
                ? McpRequiredDecision.ImportValidItems
                : McpRequiredDecision.ApplyChanges;

    private static string DecisionWire(McpRequiredDecision decision) =>
        decision switch
        {
            McpRequiredDecision.ApplyChanges => "APPLY_CHANGES",
            McpRequiredDecision.DeletePermanently => "DELETE_PERMANENTLY",
            McpRequiredDecision.ImportValidItems => "IMPORT_VALID_ITEMS",
            _ => throw new ArgumentOutOfRangeException(nameof(decision))
        };

    private static string SafeOperationSummary(McpWriteCommand command) =>
        command.Action switch
        {
            McpPreviewAction.Create => "Registro criado conforme a prévia confirmada.",
            McpPreviewAction.Update => "Registro alterado conforme a prévia confirmada.",
            McpPreviewAction.Delete => "Registro excluído definitivamente conforme a prévia confirmada.",
            _ => "Operação concluída conforme a prévia confirmada."
        };

    private static string EntityWire(McpWriteEntity entity) =>
        entity switch
        {
            McpWriteEntity.Category => "category",
            McpWriteEntity.Income => "income",
            McpWriteEntity.Expense => "expense",
            McpWriteEntity.Investment => "investment",
            McpWriteEntity.FixedCost => "fixed_cost",
            _ => throw new ArgumentOutOfRangeException(nameof(entity))
        };

    private static IReadOnlyDictionary<string, object?> Origin(McpCallContext context) =>
        new Dictionary<string, object?>
        {
            ["clientId"] = context.ClientId,
            ["protocolRevision"] = context.ProtocolRevision,
            ["channel"] = "mcp"
        };

    private DateTime UtcNow() => _time.GetUtcNow().UtcDateTime;
}
