[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$EnvFile
)

$ErrorActionPreference = "Stop"
$composeFile = Join-Path $PSScriptRoot "..\docker-compose.mcp-staging.yaml"
$resolvedEnv = (Resolve-Path -LiteralPath $EnvFile).Path

& (Join-Path $PSScriptRoot "mcp-local-config.ps1") -EnvFile $resolvedEnv
docker compose --profile observability --env-file $resolvedEnv -f $composeFile up -d --build --wait
if ($LASTEXITCODE -ne 0) {
    docker compose --profile observability --env-file $resolvedEnv -f $composeFile ps
    throw "Homologação local não ficou saudável."
}

docker compose --profile observability --env-file $resolvedEnv -f $composeFile ps
Write-Output "LOCAL_UP_OK"
