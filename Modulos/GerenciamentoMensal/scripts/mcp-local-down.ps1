[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$EnvFile
)

$ErrorActionPreference = "Stop"
$composeFile = Join-Path $PSScriptRoot "..\docker-compose.mcp-staging.yaml"
$resolvedEnv = (Resolve-Path -LiteralPath $EnvFile).Path

# Não usa -v: Mongo, Prometheus, Grafana, journals e auditoria permanecem.
docker compose --profile observability --env-file $resolvedEnv -f $composeFile down --remove-orphans
if ($LASTEXITCODE -ne 0) {
    throw "Falha ao encerrar a homologação local."
}
Write-Output "LOCAL_DOWN_OK volumes=preserved"
