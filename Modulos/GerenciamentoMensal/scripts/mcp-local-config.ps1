[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$EnvFile
)

$ErrorActionPreference = "Stop"
$composeFile = Join-Path $PSScriptRoot "..\docker-compose.mcp-staging.yaml"
$resolvedEnv = (Resolve-Path -LiteralPath $EnvFile).Path
$content = Get-Content -LiteralPath $resolvedEnv
$values = @{}
$content | ForEach-Object {
    if ($_ -match "^([^#=]+)=(.*)$") {
        $values[$Matches[1]] = $Matches[2]
    }
}

if ($content | Select-String -Pattern "CHANGE_ME") {
    throw "O arquivo local ainda contém CHANGE_ME."
}

$publicBaseUrl = $values.MCP_PUBLIC_BASE_URL
if (-not $publicBaseUrl -or $publicBaseUrl -notmatch "^https://") {
    throw "MCP_PUBLIC_BASE_URL deve usar https:// no ambiente local."
}

$jwtIssuer = $values.JWT_ISSUER
if (-not $jwtIssuer -or $jwtIssuer -notmatch "^https://") {
    throw "JWT_ISSUER deve usar https:// no ambiente local."
}

$homologUrlApi = $values.HOMOLOG_URL_API
if (-not $homologUrlApi -or $homologUrlApi -notmatch "^https://") {
    throw "HOMOLOG_URL_API deve usar https:// no ambiente local."
}

$certificateDirectory = $values.MCP_CERTIFICATES_DIR
if (-not $certificateDirectory -or
    -not (Test-Path -LiteralPath (Join-Path $certificateDirectory "mcp-signing.pfx")) -or
    -not (Test-Path -LiteralPath (Join-Path $certificateDirectory "mcp-encryption.pfx")) -or
    -not (Test-Path -LiteralPath (Join-Path $certificateDirectory "mcp-local-https.pfx"))) {
    throw "Certificados locais de assinatura, criptografia e HTTPS não encontrados."
}

if (-not $values.MCP_HTTPS_CERTIFICATE_PASSWORD) {
    throw "MCP_HTTPS_CERTIFICATE_PASSWORD é obrigatória para HTTPS local."
}

docker compose --profile observability --env-file $resolvedEnv -f $composeFile config --quiet
if ($LASTEXITCODE -ne 0) {
    throw "Configuração do Compose inválida."
}

Write-Output "CONFIG_OK env=$resolvedEnv"
