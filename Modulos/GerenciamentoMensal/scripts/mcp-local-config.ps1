[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$EnvFile
)

$ErrorActionPreference = "Stop"
$composeFile = Join-Path $PSScriptRoot "..\docker-compose.mcp-staging.yaml"
$resolvedEnv = (Resolve-Path -LiteralPath $EnvFile).Path
$content = Get-Content -LiteralPath $resolvedEnv

if ($content | Select-String -Pattern "CHANGE_ME") {
    throw "O arquivo local ainda contém CHANGE_ME."
}

$certificateDirectory = ($content |
    Where-Object { $_ -match "^MCP_CERTIFICATES_DIR=(.+)$" } |
    Select-Object -First 1) -replace "^MCP_CERTIFICATES_DIR=", ""
if (-not $certificateDirectory -or
    -not (Test-Path -LiteralPath (Join-Path $certificateDirectory "mcp-signing.pfx")) -or
    -not (Test-Path -LiteralPath (Join-Path $certificateDirectory "mcp-encryption.pfx"))) {
    throw "Certificados locais de assinatura e criptografia não encontrados."
}

docker compose --profile observability --env-file $resolvedEnv -f $composeFile config --quiet
if ($LASTEXITCODE -ne 0) {
    throw "Configuração do Compose inválida."
}

Write-Output "CONFIG_OK env=$resolvedEnv"
