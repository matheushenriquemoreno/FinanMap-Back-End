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

& (Join-Path $PSScriptRoot "mcp-local-config.ps1") -EnvFile $resolvedEnvFile
docker compose --profile observability --env-file $resolvedEnvFile -f $composeFile build --pull webapi
if ($LASTEXITCODE -ne 0) { throw "Build local falhou." }
docker compose --profile observability --env-file $resolvedEnvFile -f $composeFile up -d --wait
if ($LASTEXITCODE -ne 0) { throw "Stack local não ficou saudável." }
& (Join-Path $PSScriptRoot "mcp-local-health.ps1") -EnvFile $resolvedEnvFile

Write-Output "SMOKE_OK api=Healthy prometheus=up grafana=ok"

if ($Cleanup) {
    # Remove contêineres e rede, mas preserva deliberadamente o volume/auditoria.
    & (Join-Path $PSScriptRoot "mcp-local-down.ps1") -EnvFile $resolvedEnvFile
}
