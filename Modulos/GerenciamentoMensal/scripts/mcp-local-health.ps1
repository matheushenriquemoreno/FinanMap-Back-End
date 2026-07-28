[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$EnvFile
)

$ErrorActionPreference = "Stop"
$resolvedEnv = (Resolve-Path -LiteralPath $EnvFile).Path
$values = @{}
Get-Content -LiteralPath $resolvedEnv | ForEach-Object {
    if ($_ -match "^([^#=]+)=(.*)$") {
        $values[$Matches[1]] = $Matches[2]
    }
}

$apiPort = if ($values.MCP_STAGING_HTTPS_PORT) { $values.MCP_STAGING_HTTPS_PORT } else { "17271" }
$prometheusPort = if ($values.PROMETHEUS_PORT) { $values.PROMETHEUS_PORT } else { "19090" }
$grafanaPort = if ($values.GRAFANA_PORT) { $values.GRAFANA_PORT } else { "13000" }

$api = Invoke-RestMethod "https://localhost:$apiPort/healthcheck" -TimeoutSec 15
if ($api.status -ne "Healthy") { throw "API não saudável: $($api.status)" }
Invoke-WebRequest "https://localhost:$apiPort/metrics" -UseBasicParsing -TimeoutSec 15 | Out-Null
Invoke-WebRequest "http://127.0.0.1:$prometheusPort/-/ready" -UseBasicParsing -TimeoutSec 15 | Out-Null
$targets = Invoke-RestMethod "http://127.0.0.1:$prometheusPort/api/v1/targets" -TimeoutSec 15
if ($targets.data.activeTargets.health -notcontains "up") { throw "Prometheus não está coletando a API." }
$grafana = Invoke-RestMethod "http://127.0.0.1:$grafanaPort/api/health" -TimeoutSec 15
if ($grafana.database -ne "ok") { throw "Grafana não saudável." }

Write-Output "HEALTH_OK api=Healthy prometheus=up grafana=ok"
