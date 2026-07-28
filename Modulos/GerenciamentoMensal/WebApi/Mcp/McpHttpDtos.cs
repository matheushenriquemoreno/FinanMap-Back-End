using Application.Mcp.Configuration;
using Domain.Mcp.Entities;
using Domain.Mcp.Enums;

namespace WebApi.Mcp;

public static class McpHttpDtoMapper
{
    public static McpConfigurationResponse MapConfiguration(McpFeatureOptions options)
    {
        var baseUrl = options.PublicBaseUrl.TrimEnd('/');
        return new McpConfigurationResponse(
            $"{baseUrl}/mcp",
            McpProtocolContract.Revision,
            McpProtocolContract.SdkVersion,
            new McpAuthorizationContract(
                McpProtocolContract.OAuthGrant,
                McpProtocolContract.PkceMethod,
                $"{baseUrl}/.well-known/oauth-protected-resource/mcp",
                $"{baseUrl}/.well-known/oauth-authorization-server"),
            [
                new McpProfileContract("read_only", McpProtocolContract.ReadOnlyScopes),
                new McpProfileContract("full_management", McpProtocolContract.FullManagementScopes)
            ],
            new McpFeatureContract(
                options.EndpointEnabled,
                options.WriteToolsEnabled,
                options.HistoryEnabled));
    }

    public static McpConnectionDto Map(McpConnection connection) =>
        new(
            connection.Id,
            connection.ClientId,
            connection.ClientName,
            connection.Scopes,
            ConnectionStatus(connection.Status),
            connection.CreatedAtUtc,
            connection.LastUsedAtUtc,
            connection.RevokedAtUtc,
            connection.RevocationReasonCode);

    public static McpAuditEventDto Map(McpOperationJournal journal) =>
        new(
            journal.Id,
            journal.CorrelationId,
            journal.ConnectionId,
            journal.ToolName,
            OperationClass(journal.OperationClass),
            AuditState(journal),
            journal.StartedAtUtc,
            journal.FinishedAtUtc,
            journal.Origin,
            journal.ResultSummary,
            journal.ErrorCodes);

    public static McpAuditEventDetailDto MapDetail(McpOperationJournal journal) =>
        new(
            journal.Id,
            journal.CorrelationId,
            journal.ConnectionId,
            journal.ToolName,
            OperationClass(journal.OperationClass),
            AuditState(journal),
            WriteAction(journal),
            journal.StartedAtUtc,
            journal.FinishedAtUtc,
            SafeResultSummary(journal.ResultSummary),
            journal.ErrorCodes,
            Preview(journal.ResultSummary),
            Confirmation(journal.ResultSummary),
            Reconciliation(journal),
            Result(journal),
            Failure(journal),
            ImportBatch(journal.ResultSummary));

    private static string ConnectionStatus(McpConnectionStatus status) => status switch
    {
        McpConnectionStatus.Pending => "pending",
        McpConnectionStatus.Active => "active",
        McpConnectionStatus.Invalid => "invalid",
        McpConnectionStatus.Revoked => "revoked",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private static string OperationClass(McpOperationClass operationClass) => operationClass switch
    {
        McpOperationClass.Read => "read",
        McpOperationClass.Preview => "preview",
        McpOperationClass.Confirm => "confirm",
        McpOperationClass.Import => "import",
        McpOperationClass.Auth => "auth",
        McpOperationClass.Revoke => "revoke",
        _ => throw new ArgumentOutOfRangeException(nameof(operationClass))
    };

    private static string OperationState(McpOperationState state) => state switch
    {
        McpOperationState.Received => "received",
        McpOperationState.Executing => "executing",
        McpOperationState.Reconciling => "reconciling",
        McpOperationState.Completed => "completed",
        McpOperationState.PartiallyCompleted => "partiallyCompleted",
        McpOperationState.Failed => "failed",
        McpOperationState.Rejected => "rejected",
        McpOperationState.Unknown => "unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };

    private static string AuditState(McpOperationJournal journal) =>
        journal.ErrorCodes.Any(code =>
            string.Equals(code, "PREVIEW_EXPIRED", StringComparison.Ordinal))
            ? "expired"
            : OperationState(journal.State);

    private static string? WriteAction(McpOperationJournal journal)
    {
        var persistedAction = GetString(journal.ResultSummary, "action");
        if (persistedAction is "create" or "update" or "delete" or "cancel" or "status")
            return persistedAction;

        var toolName = journal.ToolName;
        if (toolName.Contains("_create_", StringComparison.Ordinal))
            return "create";
        if (toolName.Contains("_update_", StringComparison.Ordinal))
            return "update";
        if (toolName.Contains("_delete_", StringComparison.Ordinal))
            return "delete";
        return null;
    }

    private static IReadOnlyDictionary<string, object?> SafeResultSummary(
        IReadOnlyDictionary<string, object?> resultSummary)
    {
        var safe = new Dictionary<string, object?>();
        CopySafeScalar(resultSummary, safe, "summary");
        CopySafeScalar(resultSummary, safe, "message");
        CopySafeScalar(resultSummary, safe, "status");
        CopySafeScalar(resultSummary, safe, "count");
        return safe;
    }

    private static McpAuditPreviewDto? Preview(
        IReadOnlyDictionary<string, object?> resultSummary)
    {
        if (!TryGetDictionary(resultSummary, "preview", out var preview))
            return null;

        var resourceType = GetString(preview, "resourceType");
        var recordReference = GetString(preview, "recordReference");
        var requiredDecision = RequiredDecision(preview);
        var expiresAtUtc = GetDateTime(preview, "expiresAtUtc");
        if (resourceType is null ||
            recordReference is null ||
            requiredDecision is null ||
            expiresAtUtc is null ||
            !TryGetBoolean(preview, "irreversible", out var irreversible))
        {
            return null;
        }

        var changes = GetDictionaries(preview, "changes")
            .Select(change =>
            {
                var field = GetString(change, "field");
                var label = GetString(change, "label");
                return field is null || label is null
                    ? null
                    : new McpAuditPreviewChangeDto(
                        field,
                        label,
                        GetSafeValue(change, "currentValue"),
                        GetSafeValue(change, "proposedValue"));
            })
            .Where(change => change is not null)
            .Cast<McpAuditPreviewChangeDto>()
            .ToArray();

        return new McpAuditPreviewDto(
            resourceType,
            recordReference,
            changes,
            irreversible,
            expiresAtUtc.Value,
            requiredDecision);
    }

    private static McpAuditConfirmationDto? Confirmation(
        IReadOnlyDictionary<string, object?> resultSummary)
    {
        if (!TryGetDictionary(resultSummary, "confirmation", out var confirmation))
            return null;

        var decision = RequiredDecision(confirmation, "decision");
        var confirmedAtUtc = GetDateTime(confirmation, "confirmedAtUtc");
        return decision is null || confirmedAtUtc is null
            ? null
            : new McpAuditConfirmationDto(
                decision,
                "resource_owner",
                confirmedAtUtc.Value);
    }

    private static McpAuditReconciliationDto? Reconciliation(
        McpOperationJournal journal)
    {
        if (!TryGetDictionary(
                journal.ResultSummary,
                "reconciliation",
                out var reconciliation))
        {
            return null;
        }

        var status = GetString(reconciliation, "status");
        var summary = GetString(reconciliation, "summary");
        if (!IsReconciliationStatus(status) || summary is null)
            return null;

        return new McpAuditReconciliationDto(
            status!,
            journal.AttemptCount,
            journal.ReconciledAtUtc,
            summary,
            GetString(reconciliation, "guidance"));
    }

    private static McpAuditWriteResultDto? Result(McpOperationJournal journal)
    {
        var summary = GetString(journal.ResultSummary, "summary");
        var persistedEntityId = GetString(journal.ResultSummary, "entityId");
        var items = Enumerable.Range(0, journal.Steps.Count)
            .Select(index =>
            {
                var step = journal.Steps[index];
                var targetReference = index < journal.TargetRefs.Count
                    ? journal.TargetRefs[index].EntityId
                    : persistedEntityId;
                return new McpAuditResultItemDto(
                    GetString(step.ResultSummary, "reference")
                        ?? targetReference
                        ?? $"step:{step.Name}",
                    ResultState(step.State),
                    GetString(step.ResultSummary, "summary")
                        ?? ResultItemSummary(step.State),
                    step.ErrorCode,
                    GetString(step.ResultSummary, "guidance"));
            })
            .ToArray();

        return summary is null && items.Length == 0
            ? null
            : new McpAuditWriteResultDto(summary ?? "Operação registrada.", items);
    }

    private static McpAuditFailureDto? Failure(McpOperationJournal journal)
    {
        if (TryGetDictionary(journal.ResultSummary, "failure", out var failure))
        {
            var code = GetString(failure, "code");
            var message = GetString(failure, "message");
            var guidance = GetString(failure, "guidance");
            if (code is not null && message is not null && guidance is not null)
                return new McpAuditFailureDto(code, message, guidance);
        }

        var errorCode = journal.ErrorCodes.FirstOrDefault();
        return errorCode switch
        {
            null => null,
            "PREVIEW_EXPIRED" => new McpAuditFailureDto(
                errorCode,
                "A prévia expirou sem confirmação.",
                "Solicite uma nova prévia antes de confirmar."),
            "CONFLICT_CHANGED" => new McpAuditFailureDto(
                errorCode,
                "O registro mudou desde a preparação da prévia.",
                "Revise os dados atuais e solicite uma nova prévia."),
            "RESULT_UNKNOWN" or "EFFECT_OUTCOME_UNKNOWN" => new McpAuditFailureDto(
                errorCode,
                "Não foi possível comprovar o resultado da escrita.",
                "Consulte o resultado desta operação antes de tentar novamente."),
            _ => new McpAuditFailureDto(
                errorCode,
                "A operação não pôde ser concluída.",
                "Revise os dados informados antes de tentar novamente.")
        };
    }

    private static McpAuditImportBatchDto? ImportBatch(
        IReadOnlyDictionary<string, object?> resultSummary)
    {
        if (!TryGetDictionary(resultSummary, "importBatch", out var batch))
            return null;

        var state = GetString(batch, "state");
        var itemCount = GetInt(batch, "itemCount");
        if (state is not ("partial" or "completed" or "failed" or "unknown") ||
            itemCount is null or < 0)
        {
            return null;
        }

        var countsByState = SafeCounts(
            batch,
            "countsByState",
            new[]
            {
                "valid", "invalid", "pending", "possible_duplicate", "skipped",
                "already_applied", "completed", "failed", "unknown"
            });
        var countsByType = SafeCounts(
            batch,
            "countsByType",
            new[] { "category", "income", "expense", "investment", "fixed_cost" });
        var totals = GetDictionaries(batch, "totals")
            .Select(item =>
            {
                var type = GetString(item, "type");
                var amount = GetDecimal(item, "amount");
                var currency = GetString(item, "currency");
                return IsImportType(type) && amount is not null && currency == "BRL"
                    ? new McpAuditImportTotalDto(type!, amount.Value, currency)
                    : null;
            })
            .Where(item => item is not null)
            .Cast<McpAuditImportTotalDto>()
            .ToArray();
        var failures = GetDictionaries(batch, "failures")
            .Select(item =>
            {
                var clientItemId = GetString(item, "clientItemId");
                var code = GetString(item, "code");
                var message = GetString(item, "message");
                var guidance = GetString(item, "guidance");
                return clientItemId is null || code is null ||
                       message is null || guidance is null
                    ? null
                    : new McpAuditImportFailureDto(
                        clientItemId,
                        GetString(item, "sourceRef"),
                        GetString(item, "field"),
                        code,
                        message,
                        guidance);
            })
            .Where(item => item is not null)
            .Cast<McpAuditImportFailureDto>()
            .ToArray();
        var items = GetDictionaries(batch, "items")
            .Select(item =>
            {
                var clientItemId = GetString(item, "clientItemId");
                var type = GetString(item, "type");
                var operationId = GetString(item, "operationId");
                var result = GetString(item, "result");
                return clientItemId is null || !IsImportType(type) ||
                       operationId is null ||
                       result is not ("completed" or "failed" or "unknown")
                    ? null
                    : new McpAuditImportItemDto(
                        clientItemId,
                        GetString(item, "sourceRef"),
                        type!,
                        operationId,
                        result);
            })
            .Where(item => item is not null)
            .Cast<McpAuditImportItemDto>()
            .ToArray();

        return new McpAuditImportBatchDto(
            state,
            itemCount.Value,
            countsByState,
            countsByType,
            totals,
            failures,
            items);
    }

    private static IReadOnlyDictionary<string, int> SafeCounts(
        IReadOnlyDictionary<string, object?> source,
        string key,
        IReadOnlyCollection<string> allowedKeys)
    {
        if (!TryGetDictionary(source, key, out var values))
            return new Dictionary<string, int>();

        return values
            .Where(item => allowedKeys.Contains(item.Key))
            .Select(item => (item.Key, Value: Integer(item.Value)))
            .Where(item => item.Value is >= 0)
            .ToDictionary(item => item.Key, item => item.Value!.Value);
    }

    private static bool IsImportType(string? type) =>
        type is "category" or "income" or "expense" or "investment" or
            "fixed_cost";

    private static string ResultState(McpOperationStepState state) => state switch
    {
        McpOperationStepState.Pending => "processing",
        McpOperationStepState.Executing => "processing",
        McpOperationStepState.Completed => "completed",
        McpOperationStepState.Failed => "failed",
        McpOperationStepState.Unknown => "unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };

    private static string ResultItemSummary(McpOperationStepState state) => state switch
    {
        McpOperationStepState.Pending => "Aguardando execução.",
        McpOperationStepState.Executing => "Execução em andamento.",
        McpOperationStepState.Completed => "Operação concluída.",
        McpOperationStepState.Failed => "A operação falhou.",
        McpOperationStepState.Unknown => "O resultado ainda não foi comprovado.",
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };

    private static bool IsReconciliationStatus(string? status) =>
        status is "not_required" or "completed" or "rejected" or "unknown" or
            "pending" or "checking" or "confirmed" or "not_applied" or
            "inconclusive";

    private static string? RequiredDecision(
        IReadOnlyDictionary<string, object?> values,
        string key = "requiredDecision")
    {
        if (!values.TryGetValue(key, out var value))
            return null;

        var decision = value switch
        {
            McpRequiredDecision.ApplyChanges => "APPLY_CHANGES",
            McpRequiredDecision.DeletePermanently => "DELETE_PERMANENTLY",
            McpRequiredDecision.ImportValidItems => "IMPORT_VALID_ITEMS",
            string text => text,
            _ => null
        };
        return decision is "APPLY_CHANGES" or "DELETE_PERMANENTLY" or
            "IMPORT_VALID_ITEMS"
            ? decision
            : null;
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> GetDictionaries(
        IReadOnlyDictionary<string, object?> values,
        string key)
    {
        if (!values.TryGetValue(key, out var value) ||
            value is not IEnumerable<object?> items)
        {
            return [];
        }

        return items
            .OfType<IReadOnlyDictionary<string, object?>>()
            .ToArray();
    }

    private static bool TryGetDictionary(
        IReadOnlyDictionary<string, object?> values,
        string key,
        out IReadOnlyDictionary<string, object?> dictionary)
    {
        if (values.TryGetValue(key, out var value) &&
            value is IReadOnlyDictionary<string, object?> typed)
        {
            dictionary = typed;
            return true;
        }

        dictionary = new Dictionary<string, object?>();
        return false;
    }

    private static string? GetString(
        IReadOnlyDictionary<string, object?>? values,
        string key) =>
        values is not null &&
        values.TryGetValue(key, out var value) &&
        value is string text &&
        !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    private static DateTime? GetDateTime(
        IReadOnlyDictionary<string, object?> values,
        string key)
    {
        if (!values.TryGetValue(key, out var value))
            return null;

        return value switch
        {
            DateTime dateTime => dateTime,
            DateTimeOffset offset => offset.UtcDateTime,
            string text when DateTime.TryParse(
                text,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var parsed) => parsed,
            _ => null
        };
    }

    private static int? GetInt(
        IReadOnlyDictionary<string, object?> values,
        string key) =>
        values.TryGetValue(key, out var value) ? Integer(value) : null;

    private static int? Integer(object? value) => value switch
    {
        byte number => number,
        short number => number,
        int number => number,
        long number when number is >= int.MinValue and <= int.MaxValue => (int)number,
        _ => null
    };

    private static decimal? GetDecimal(
        IReadOnlyDictionary<string, object?> values,
        string key)
    {
        if (!values.TryGetValue(key, out var value))
            return null;
        return value switch
        {
            decimal number => number,
            int number => number,
            long number => number,
            double number => Convert.ToDecimal(number),
            _ => null
        };
    }

    private static bool TryGetBoolean(
        IReadOnlyDictionary<string, object?> values,
        string key,
        out bool result)
    {
        if (values.TryGetValue(key, out var value) && value is bool boolean)
        {
            result = boolean;
            return true;
        }

        result = false;
        return false;
    }

    private static object? GetSafeValue(
        IReadOnlyDictionary<string, object?> values,
        string key)
    {
        if (!values.TryGetValue(key, out var value))
            return null;

        return value switch
        {
            null => null,
            string => value,
            bool => value,
            byte => value,
            sbyte => value,
            short => value,
            ushort => value,
            int => value,
            uint => value,
            long => value,
            ulong => value,
            float => value,
            double => value,
            decimal => value,
            DateTime dateTime => dateTime.ToUniversalTime().ToString("O"),
            DateTimeOffset offset => offset.UtcDateTime.ToString("O"),
            _ => null
        };
    }

    private static void CopySafeScalar(
        IReadOnlyDictionary<string, object?> source,
        IDictionary<string, object?> destination,
        string key)
    {
        if (!source.ContainsKey(key))
            return;

        var value = GetSafeValue(source, key);
        if (value is not null || source[key] is null)
            destination[key] = value;
    }
}

public sealed record McpConfigurationResponse(
    string Endpoint,
    string ProtocolRevision,
    string SdkVersion,
    McpAuthorizationContract Authorization,
    IReadOnlyList<McpProfileContract> Profiles,
    McpFeatureContract Features);

public sealed record McpAuthorizationContract(
    string GrantType,
    string PkceMethod,
    string ProtectedResourceMetadataUrl,
    string AuthorizationServerMetadataUrl);

public sealed record McpProfileContract(string Id, IReadOnlyList<string> Scopes);

public sealed record McpFeatureContract(
    bool EndpointEnabled,
    bool WriteToolsEnabled,
    bool HistoryEnabled);

public sealed record McpConnectionDto(
    string Id,
    string ClientId,
    string ClientName,
    IReadOnlyList<string> Scopes,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? LastUsedAtUtc,
    DateTime? RevokedAtUtc,
    string? RevocationReasonCode);

public sealed record McpAuditEventDto(
    string Id,
    string CorrelationId,
    string ConnectionId,
    string ToolName,
    string OperationClass,
    string State,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    IReadOnlyDictionary<string, object?> Origin,
    IReadOnlyDictionary<string, object?> ResultSummary,
    IReadOnlyList<string> ErrorCodes);

public sealed record McpAuditEventDetailDto(
    string Id,
    string CorrelationId,
    string ConnectionId,
    string ToolName,
    string OperationClass,
    string State,
    string? Action,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    IReadOnlyDictionary<string, object?> ResultSummary,
    IReadOnlyList<string> ErrorCodes,
    McpAuditPreviewDto? Preview,
    McpAuditConfirmationDto? Confirmation,
    McpAuditReconciliationDto? Reconciliation,
    McpAuditWriteResultDto? Result,
    McpAuditFailureDto? Failure,
    McpAuditImportBatchDto? ImportBatch);

public sealed record McpAuditPreviewChangeDto(
    string Field,
    string Label,
    object? CurrentValue,
    object? ProposedValue);

public sealed record McpAuditPreviewDto(
    string ResourceType,
    string RecordReference,
    IReadOnlyList<McpAuditPreviewChangeDto> Changes,
    bool Irreversible,
    DateTime ExpiresAtUtc,
    string RequiredDecision);

public sealed record McpAuditConfirmationDto(
    string Decision,
    string ConfirmedBy,
    DateTime ConfirmedAtUtc);

public sealed record McpAuditReconciliationDto(
    string Status,
    int Attempts,
    DateTime? LastCheckedAtUtc,
    string Summary,
    string? Guidance);

public sealed record McpAuditResultItemDto(
    string Reference,
    string Status,
    string Summary,
    string? ErrorCode,
    string? Guidance);

public sealed record McpAuditWriteResultDto(
    string Summary,
    IReadOnlyList<McpAuditResultItemDto> Items);

public sealed record McpAuditFailureDto(
    string Code,
    string Message,
    string Guidance);

public sealed record McpAuditImportBatchDto(
    string State,
    int ItemCount,
    IReadOnlyDictionary<string, int> CountsByState,
    IReadOnlyDictionary<string, int> CountsByType,
    IReadOnlyList<McpAuditImportTotalDto> Totals,
    IReadOnlyList<McpAuditImportFailureDto> Failures,
    IReadOnlyList<McpAuditImportItemDto> Items);

public sealed record McpAuditImportTotalDto(
    string Type,
    decimal Amount,
    string Currency);

public sealed record McpAuditImportFailureDto(
    string ClientItemId,
    string? SourceRef,
    string? Field,
    string Code,
    string Message,
    string Guidance);

public sealed record McpAuditImportItemDto(
    string ClientItemId,
    string? SourceRef,
    string Type,
    string OperationId,
    string Result);

public sealed record McpListResponse<T>(IReadOnlyList<T> Items, string? NextCursor = null);
public sealed record McpRevokeRequest(string? ReasonCode);
public sealed record McpApproveRequest(IReadOnlyList<string> Scopes);
public sealed record McpDenyRequest(string? ReasonCode);
