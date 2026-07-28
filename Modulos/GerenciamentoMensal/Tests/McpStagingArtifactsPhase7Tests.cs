using System.Text.RegularExpressions;
using Xunit;

namespace Tests;

public sealed class McpStagingArtifactsPhase7Tests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string ModuleRoot =
        Path.Combine(RepositoryRoot, "Modulos", "GerenciamentoMensal");

    [Fact]
    public void Staging_compose_is_pinned_secretless_and_health_checked()
    {
        var compose = ReadModuleFile("docker-compose.mcp-staging.yaml");

        Assert.Contains("mongo@sha256:", compose, StringComparison.Ordinal);
        Assert.Contains("${MCP_STAGING_IMAGE:", compose, StringComparison.Ordinal);
        Assert.Contains("${MONGO_ROOT_PASSWORD:?", compose, StringComparison.Ordinal);
        Assert.Contains("${JWT_KEY:?", compose, StringComparison.Ordinal);
        Assert.Contains("${MCP_CURSOR_SIGNING_KEY:?", compose, StringComparison.Ordinal);
        Assert.Contains("/healthcheck", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("123456", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("SuperSecretPassword", compose, StringComparison.Ordinal);
    }

    [Fact]
    public void Staging_example_and_scripts_do_not_embed_secrets_or_destroy_data()
    {
        var example = ReadModuleFile(".env.mcp-staging.example");
        var smoke = ReadModuleFile("scripts", "mcp-staging-smoke.ps1");
        var rollback = ReadModuleFile("scripts", "mcp-staging-rollback.ps1");

        Assert.Contains("CHANGE_ME", example, StringComparison.Ordinal);
        Assert.DoesNotMatch(
            new Regex(@"JWT_KEY=(?!CHANGE_ME|$).+", RegexOptions.Multiline),
            example);
        Assert.Contains("docker compose", smoke, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PreviousImage", rollback, StringComparison.Ordinal);
        Assert.Contains("MCP_WRITE_TOOLS_ENABLED=false", rollback, StringComparison.Ordinal);
        Assert.Contains("MCP_FEATURE_ENABLED=false", rollback, StringComparison.Ordinal);
        Assert.DoesNotContain("down -v", rollback, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("docker volume rm", rollback, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Operational_alerts_cover_the_minimum_phase_7_signals()
    {
        var alerts = ReadModuleFile("ops", "mcp", "prometheus-alerts.yaml");
        var dashboard = ReadModuleFile("ops", "mcp", "grafana-dashboard.json");

        var requiredSignals = new[]
        {
            "mcp_journal_write_failure_total",
            "mcp_tool_calls_total",
            "mcp_tool_duration_ms",
            "mcp_confirmation_replay_total",
            "mcp_auth_denied_total",
            "mcp_unknown_operations_total",
            "mcp_expired_leases_total"
        };

        foreach (var signal in requiredSignals)
        {
            Assert.Contains(signal, alerts + dashboard, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Traceability_covers_ten_journeys_and_all_125_requirements()
    {
        var traceability = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            ".specs",
            "mcp-financeiro-conversacional",
            "PHASE-7-TRACEABILITY.md"));

        for (var journey = 1; journey <= 10; journey++)
        {
            Assert.Contains($"J{journey:00}", traceability, StringComparison.Ordinal);
        }

        for (var requirement = 1; requirement <= 125; requirement++)
        {
            Assert.Contains($"MCP-{requirement:00}", traceability, StringComparison.Ordinal);
        }

        Assert.Contains("não executada", traceability, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("homologação externa", traceability, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadModuleFile(params string[] parts)
    {
        return File.ReadAllText(Path.Combine(new[] { ModuleRoot }.Concat(parts).ToArray()));
    }

    private static string FindRepositoryRoot()
    {
        var candidates = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var candidate in candidates)
        {
            var current = new DirectoryInfo(candidate);
            while (current is not null)
            {
                if (Directory.Exists(Path.Combine(
                        current.FullName,
                        ".specs",
                        "mcp-financeiro-conversacional")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException("Raiz do repositório não encontrada.");
    }
}
