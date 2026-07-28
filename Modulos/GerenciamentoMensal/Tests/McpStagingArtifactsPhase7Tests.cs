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

    [Fact]
    public void Local_homologation_provisions_metrics_dashboard_and_full_lifecycle()
    {
        var compose = ReadModuleFile("docker-compose.mcp-staging.yaml");
        var program = ReadModuleFile("WebApi", "Program.cs");
        var project = ReadModuleFile("WebApi", "WebApi.csproj");

        Assert.Contains("prometheus:", compose, StringComparison.Ordinal);
        Assert.Contains("grafana:", compose, StringComparison.Ordinal);
        Assert.Contains("profiles: [\"observability\"]", compose, StringComparison.Ordinal);
        Assert.Contains("prometheus.yml", compose, StringComparison.Ordinal);
        Assert.Contains("provisioning", compose, StringComparison.Ordinal);
        Assert.Contains("MapPrometheusScrapingEndpoint", program, StringComparison.Ordinal);
        Assert.Contains(
            "OpenTelemetry.Exporter.Prometheus.AspNetCore",
            project,
            StringComparison.Ordinal);

        var scripts = new[]
        {
            "mcp-local-config.ps1",
            "mcp-local-up.ps1",
            "mcp-local-health.ps1",
            "mcp-local-journeys.ps1",
            "mcp-local-latency.ps1",
            "mcp-staging-rollback.ps1",
            "mcp-local-down.ps1"
        };
        foreach (var script in scripts)
        {
            Assert.True(
                File.Exists(Path.Combine(ModuleRoot, "scripts", script)),
                $"Script obrigatório ausente: {script}");
        }

        Assert.True(File.Exists(Path.Combine(
            RepositoryRoot,
            ".specs",
            "mcp-financeiro-conversacional",
            "RUNBOOK-LOCAL-HOMOLOG.md")));
    }

    [Fact]
    public void Local_mcp_configuration_exposes_only_https_public_urls()
    {
        var compose = ReadModuleFile("docker-compose.mcp-staging.yaml");
        var example = ReadModuleFile(".env.mcp-staging.example");
        var configScript = ReadModuleFile("scripts", "mcp-local-config.ps1");
        var healthScript = ReadModuleFile("scripts", "mcp-local-health.ps1");
        var prometheus = ReadModuleFile("ops", "mcp", "prometheus.yml");

        Assert.Contains("${MCP_STAGING_HTTPS_PORT:-17271}:8443", compose);
        Assert.Contains("ASPNETCORE_HTTPS_PORTS: 8443", compose);
        Assert.Contains(
            "ASPNETCORE_Kestrel__Certificates__Default__Path",
            compose);
        Assert.Contains("MCP_HTTPS_CERTIFICATE_PASSWORD", compose);
        Assert.Contains("MCP_STAGING_HTTPS_PORT=17271", example);
        Assert.Contains("HOMOLOG_URL_API=https://localhost:17271/api/", example);
        Assert.Contains("JWT_ISSUER=https://localhost:17271", example);
        Assert.Contains("MCP_PUBLIC_BASE_URL=https://localhost:17271", example);
        Assert.DoesNotContain("MCP_PUBLIC_BASE_URL=http://", example);
        Assert.DoesNotContain("JWT_ISSUER=http://", example);
        Assert.Contains("MCP_PUBLIC_BASE_URL deve usar https://", configScript);
        Assert.Contains("mcp-local-https.pfx", configScript);
        Assert.Contains("MCP_STAGING_HTTPS_PORT", healthScript);
        Assert.Contains("https://localhost:$apiPort/healthcheck", healthScript);
        Assert.Contains("scheme: https", prometheus);
        Assert.Contains("insecure_skip_verify: true", prometheus);
        Assert.Contains("webapi:8443", prometheus);
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
