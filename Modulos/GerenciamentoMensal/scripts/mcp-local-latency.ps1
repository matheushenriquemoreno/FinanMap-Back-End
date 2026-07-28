[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$moduleRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Push-Location $moduleRoot
try {
    dotnet test Tests/Tests.csproj --configuration Release --no-restore `
        --filter "FullyQualifiedName~McpLatencyGatePhase6Tests"
    if ($LASTEXITCODE -ne 0) {
        throw "Gate de latência MCP falhou."
    }
    Write-Output "LATENCY_OK read_p95<=3s write_p95<=5s"
}
finally {
    Pop-Location
}
