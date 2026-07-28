[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$EnvFile,
    [switch]$Cleanup
)

$ErrorActionPreference = "Stop"
$composeFile = Join-Path $PSScriptRoot "..\docker-compose.mcp-staging.yaml"
$resolvedEnvFile = (Resolve-Path -LiteralPath $EnvFile).Path

if (Select-String -LiteralPath $resolvedEnvFile -Pattern "CHANGE_ME" -Quiet) {
    throw "O arquivo de ambiente ainda contém CHANGE_ME."
}

docker compose --env-file $resolvedEnvFile -f $composeFile config --quiet
docker compose --env-file $resolvedEnvFile -f $composeFile build --pull webapi
docker compose --env-file $resolvedEnvFile -f $composeFile up -d --wait

$portLine = Select-String -LiteralPath $resolvedEnvFile -Pattern "^MCP_STAGING_HTTP_PORT=(.+)$"
$port = if ($portLine) { $portLine.Matches[0].Groups[1].Value } else { "17270" }
$health = Invoke-RestMethod -Uri "http://127.0.0.1:$port/healthcheck" -TimeoutSec 10
if ($health.status -ne "Healthy") {
    throw "Healthcheck MCP não saudável: $($health.status)"
}

Write-Output "SMOKE_OK health=$($health.status) url=http://127.0.0.1:$port/healthcheck"

if ($Cleanup) {
    # Remove contêineres e rede, mas preserva deliberadamente o volume/auditoria.
    docker compose --env-file $resolvedEnvFile -f $composeFile down
}
