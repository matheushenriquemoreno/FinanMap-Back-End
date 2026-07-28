[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$EnvFile,
    [Parameter(Mandatory = $true)]
    [string]$PreviousImage,
    [switch]$Execute
)

$ErrorActionPreference = "Stop"
$composeFile = Join-Path $PSScriptRoot "..\docker-compose.mcp-staging.yaml"
$resolvedEnvFile = (Resolve-Path -LiteralPath $EnvFile).Path

$steps = @(
    "MCP_WRITE_TOOLS_ENABLED=false",
    "MCP_FEATURE_ENABLED=false",
    "MCP_STAGING_IMAGE=$PreviousImage",
    "docker compose --env-file `"$resolvedEnvFile`" -f `"$composeFile`" up -d --no-build --force-recreate webapi"
)
$steps | ForEach-Object { Write-Output "ROLLBACK_PLAN $_" }

if (-not $Execute) {
    Write-Output "DRY_RUN_OK: volume Mongo, journals, auditoria e registros serão preservados."
    return
}

if ($PSCmdlet.ShouldProcess("homologação MCP", "desabilitar writes/endpoint e restaurar $PreviousImage")) {
    $env:MCP_WRITE_TOOLS_ENABLED = "false"
    $env:MCP_FEATURE_ENABLED = "false"
    $env:MCP_STAGING_IMAGE = $PreviousImage
    docker compose --env-file $resolvedEnvFile -f $composeFile up -d --no-build --force-recreate webapi
    docker compose --env-file $resolvedEnvFile -f $composeFile ps
}
