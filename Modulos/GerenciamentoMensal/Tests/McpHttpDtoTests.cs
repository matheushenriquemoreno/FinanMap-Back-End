using System.Text.Json;
using Application.Mcp.Configuration;
using Domain.Mcp.Entities;
using Domain.Mcp.Enums;
using WebApi.Mcp;
using Xunit;

namespace Tests;

public class McpHttpDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Connection_and_audit_enums_use_the_lower_camel_contract()
    {
        var connection = McpConnection.CreateActive(
            "owner-a", "authorization-a", "client-a", "Cliente", ["mcp:read"]);
        var journal = McpOperationJournal.Start(
            "owner-a", connection.Id, "correlation-a",
            "finanmap_categories_list", McpOperationClass.Read);
        journal.Complete();

        var connectionJson = JsonSerializer.Serialize(McpHttpDtoMapper.Map(connection), JsonOptions);
        var auditJson = JsonSerializer.Serialize(McpHttpDtoMapper.Map(journal), JsonOptions);

        Assert.Contains("\"status\":\"active\"", connectionJson);
        Assert.Contains("\"operationClass\":\"read\"", auditJson);
        Assert.Contains("\"state\":\"completed\"", auditJson);
    }

    [Fact]
    public void Configuration_contract_exposes_pinned_protocol_profiles_and_independent_features()
    {
        var options = new McpFeatureOptions
        {
            EndpointEnabled = true,
            WriteToolsEnabled = false,
            HistoryEnabled = true,
            PublicBaseUrl = "https://api.example"
        };

        var response = McpHttpDtoMapper.MapConfiguration(options);

        Assert.Equal("https://api.example/mcp", response.Endpoint);
        Assert.Equal("2025-11-25", response.ProtocolRevision);
        Assert.Equal("S256", response.Authorization.PkceMethod);
        Assert.Equal(["mcp:read", "mcp:audit"], response.Profiles[0].Scopes);
        Assert.True(response.Features.EndpointEnabled);
        Assert.False(response.Features.WriteToolsEnabled);
        Assert.True(response.Features.HistoryEnabled);
    }

    [Fact]
    public void OAuth_continuation_preserves_redirect_state_resource_and_pkce_challenge()
    {
        var interaction = new McpAuthorizationInteraction(
            "interaction-a",
            "client-a",
            "Cliente",
            ["mcp:read"],
            DateTime.UtcNow.AddMinutes(5),
            "https://agent.example/callback",
            "opaque-state",
            "pkce-challenge",
            "S256",
            "https://api.example/mcp");
        interaction.Approve("owner-a", ["mcp:read"], DateTime.UtcNow);
        var connection = McpConnection.CreateActive(
            "owner-a", "authorization-a", "client-a", "Cliente", ["mcp:read"]);
        interaction.BindConnection(connection.Id);

        var continuation = McpOAuthAuthorizeEndpoint.BuildContinuationUrl(
            new McpFeatureOptions { PublicBaseUrl = "https://api.example" },
            connection,
            interaction);

        Assert.Contains("redirect_uri=https%3A%2F%2Fagent.example%2Fcallback", continuation);
        Assert.Contains("state=opaque-state", continuation);
        Assert.Contains("code_challenge=pkce-challenge", continuation);
        Assert.Contains("code_challenge_method=S256", continuation);
        Assert.Contains("resource=https%3A%2F%2Fapi.example%2Fmcp", continuation);
        Assert.Contains("interaction_id=interaction-a", continuation);
    }

    [Fact]
    public void Denial_continuation_returns_registered_callback_with_state_and_no_code_or_token()
    {
        var interaction = new McpAuthorizationInteraction(
            "interaction-a",
            "client-a",
            "Cliente",
            ["mcp:read"],
            DateTime.UtcNow.AddMinutes(5),
            "https://agent.example/callback?source=inspector",
            "opaque-state",
            "pkce-challenge",
            "S256",
            "https://api.example/mcp");

        var continuation = McpOAuthAuthorizeEndpoint.BuildDeniedContinuationUrl(interaction);
        var uri = new Uri(continuation);

        Assert.Equal("https://agent.example/callback", uri.GetLeftPart(UriPartial.Path));
        Assert.Contains("source=inspector", uri.Query);
        Assert.Contains("error=access_denied", uri.Query);
        Assert.Contains("state=opaque-state", uri.Query);
        Assert.DoesNotContain("code=", uri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", uri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("challenge", uri.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Authorization_interaction_cannot_be_claimed_or_completed_by_another_owner()
    {
        var interaction = new McpAuthorizationInteraction(
            "interaction-a",
            "client-a",
            "Cliente",
            ["mcp:read"],
            DateTime.UtcNow.AddMinutes(5));
        interaction.Claim("owner-a", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            interaction.Deny("owner-b", DateTime.UtcNow));
    }

    [Fact]
    public void Audit_detail_maps_only_the_safe_write_allowlist()
    {
        var startedAt = new DateTime(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);
        var journal = McpOperationJournal.Start(
            "owner-a",
            "connection-a",
            "correlation-a",
            "finanmap_category_update_confirm",
            McpOperationClass.Confirm,
            targetRefs: [new McpTargetRef("category", "category-a")],
            steps:
            [
                new McpOperationStep(
                    "apply",
                    McpOperationStepState.Pending,
                    null,
                    null,
                    null)
            ],
            startedAtUtc: startedAt);
        Assert.True(journal.TryAcquireLease(
            "worker-a",
            startedAt.AddMinutes(1),
            TimeSpan.FromMinutes(1)));
        journal.StartStep("apply", "worker-a", startedAt.AddMinutes(1));
        journal.CompleteStep(
            "apply",
            "effect-marker-a",
            new Dictionary<string, object?>
            {
                ["reference"] = "Categoria selecionada",
                ["summary"] = "Alteração concluída.",
                ["guidance"] = "Nenhuma ação adicional é necessária.",
                ["rawPayload"] = "STEP_PAYLOAD_CANARY"
            },
            startedAt.AddMinutes(1).AddSeconds(1));
        journal.CompleteAt(
            new Dictionary<string, object?>
            {
                ["summary"] = "Categoria alterada com sucesso.",
                ["preview"] = new Dictionary<string, object?>
                {
                    ["resourceType"] = "Categoria",
                    ["recordReference"] = "Categoria selecionada",
                    ["changes"] = new object?[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["field"] = "nome",
                            ["label"] = "Nome",
                            ["currentValue"] = "Moradia",
                            ["proposedValue"] = "Casa",
                            ["internalSnapshot"] = "SNAPSHOT_CANARY"
                        }
                    },
                    ["irreversible"] = false,
                    ["expiresAtUtc"] = startedAt.AddMinutes(15),
                    ["requiredDecision"] = "APPLY_CHANGES",
                    ["payload"] = "PREVIEW_PAYLOAD_CANARY"
                },
                ["confirmation"] = new Dictionary<string, object?>
                {
                    ["decision"] = "APPLY_CHANGES",
                    ["confirmedBy"] = "client-or-worker",
                    ["confirmedAtUtc"] = startedAt.AddMinutes(1)
                },
                ["reconciliation"] = new Dictionary<string, object?>
                {
                    ["status"] = "confirmed",
                    ["summary"] = "O marcador da operação confirmou o efeito.",
                    ["guidance"] = "Resultado confirmado."
                },
                ["failure"] = new Dictionary<string, object?>
                {
                    ["code"] = "KNOWN_FAILURE",
                    ["message"] = "Falha conhecida e segura.",
                    ["guidance"] = "Revise os dados antes de tentar novamente.",
                    ["stackTrace"] = "STACK_CANARY"
                },
                ["accessToken"] = "TOKEN_CANARY",
                ["sanitizedParameters"] = "PARAMETERS_CANARY"
            },
            startedAt.AddMinutes(2),
            reconciled: true);

        var detail = McpHttpDtoMapper.MapDetail(journal);
        var json = JsonSerializer.Serialize(detail, JsonOptions);

        Assert.Equal("update", detail.Action);
        Assert.Equal("completed", detail.State);
        Assert.Equal("Categoria", detail.Preview?.ResourceType);
        Assert.Equal("Categoria selecionada", detail.Preview?.RecordReference);
        Assert.Equal("nome", Assert.Single(detail.Preview!.Changes).Field);
        Assert.Equal("resource_owner", detail.Confirmation?.ConfirmedBy);
        Assert.Equal(1, detail.Reconciliation?.Attempts);
        Assert.Equal(startedAt.AddMinutes(2), detail.Reconciliation?.LastCheckedAtUtc);
        Assert.Equal("Categoria alterada com sucesso.", detail.Result?.Summary);
        Assert.Equal(
            "Categoria selecionada",
            Assert.Single(detail.Result!.Items).Reference);
        Assert.Equal("completed", Assert.Single(detail.Result.Items).Status);
        Assert.Equal("KNOWN_FAILURE", detail.Failure?.Code);
        Assert.DoesNotContain(
            "STEP_PAYLOAD_CANARY",
            json,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "SNAPSHOT_CANARY",
            json,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "PREVIEW_PAYLOAD_CANARY",
            json,
            StringComparison.Ordinal);
        Assert.DoesNotContain("STACK_CANARY", json, StringComparison.Ordinal);
        Assert.DoesNotContain("TOKEN_CANARY", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PARAMETERS_CANARY", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Audit_detail_exposes_expiration_without_changing_the_list_contract()
    {
        var startedAt = new DateTime(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);
        var journal = McpOperationJournal.Start(
            "owner-a",
            "connection-a",
            "correlation-a",
            "finanmap_income_delete_preview",
            McpOperationClass.Preview,
            origin: new Dictionary<string, object?>
            {
                ["accessToken"] = "ORIGIN_TOKEN_CANARY"
            },
            startedAtUtc: startedAt);
        journal.Reject("PREVIEW_EXPIRED", startedAt.AddMinutes(16));

        var listJson = JsonSerializer.Serialize(
            McpHttpDtoMapper.Map(journal),
            JsonOptions);
        var detail = McpHttpDtoMapper.MapDetail(journal);
        var detailJson = JsonSerializer.Serialize(detail, JsonOptions);

        Assert.Equal("expired", detail.State);
        Assert.Equal("delete", detail.Action);
        Assert.Equal("PREVIEW_EXPIRED", detail.Failure?.Code);
        Assert.Contains("expirou", detail.Failure?.Message);
        Assert.Contains("nova prévia", detail.Failure?.Guidance);
        Assert.DoesNotContain("\"preview\":", listJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"confirmation\":", listJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"reconciliation\":", listJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"result\":", listJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"failure\":", listJson, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ORIGIN_TOKEN_CANARY",
            detailJson,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Confirm_detail_uses_the_persisted_result_action_and_maps_steps_without_initial_targets()
    {
        var startedAt = new DateTime(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);
        var journal = McpOperationJournal.Start(
            "owner-a",
            "connection-a",
            "correlation-a",
            "finanmap_operation_confirm",
            McpOperationClass.Confirm,
            steps:
            [
                new McpOperationStep(
                    "apply",
                    McpOperationStepState.Pending,
                    null,
                    null,
                    null)
            ],
            startedAtUtc: startedAt);
        Assert.True(journal.TryAcquireLease(
            "worker-a",
            startedAt,
            TimeSpan.FromMinutes(1)));
        journal.StartStep("apply", "worker-a", startedAt);
        journal.CompleteStep(
            "apply",
            "effect-marker-a",
            new Dictionary<string, object?>
            {
                ["summary"] = "Categoria criada."
            },
            startedAt.AddSeconds(1));
        journal.CompleteAt(
            new Dictionary<string, object?>
            {
                ["action"] = "create",
                ["entityType"] = "category",
                ["entityId"] = "category-a",
                ["summary"] = "Categoria criada com sucesso."
            },
            startedAt.AddSeconds(1));

        var detail = McpHttpDtoMapper.MapDetail(journal);

        Assert.Equal("create", detail.Action);
        var item = Assert.Single(detail.Result!.Items);
        Assert.Equal("category-a", item.Reference);
        Assert.Equal("completed", item.Status);
    }

    [Theory]
    [InlineData("not_required")]
    [InlineData("completed")]
    [InlineData("rejected")]
    [InlineData("unknown")]
    public void Audit_detail_accepts_canonical_reconciliation_states(string status)
    {
        var journal = McpOperationJournal.Start(
            "owner-a",
            "connection-a",
            "correlation-a",
            "finanmap_operation_confirm",
            McpOperationClass.Confirm);
        journal.Complete(new Dictionary<string, object?>
        {
            ["summary"] = "Operação registrada.",
            ["reconciliation"] = new Dictionary<string, object?>
            {
                ["status"] = status,
                ["summary"] = "Estado de reconciliação persistido."
            }
        });

        var detail = McpHttpDtoMapper.MapDetail(journal);

        Assert.Equal(status, detail.Reconciliation?.Status);
    }
}
