[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$moduleRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$journeys = [ordered]@{
    J01 = "McpOAuthHostContractTests"
    J02 = "McpCategoriesToolServiceTests"
    J03 = "McpFinancialReadSourceIntegrationTests"
    J04 = "McpFinancialReadPhase2Tests"
    J05 = "McpMongoWritePhase3IntegrationTests"
    J06 = "McpOperationReconcilerPhase3Tests"
    J07 = "McpApplicationMutationServicePhase3Tests"
    J08 = "McpImportServicePhase5Tests"
    J09 = "McpMongoAuditIntegrationTests"
    J10 = "McpAuthenticatedInspectorSmokeTests"
}

Push-Location $moduleRoot
try {
    foreach ($journey in $journeys.GetEnumerator()) {
        dotnet test Tests/Tests.csproj --configuration Release --no-restore `
            --filter "FullyQualifiedName~$($journey.Value)"
        if ($LASTEXITCODE -ne 0) {
            throw "$($journey.Key) falhou: $($journey.Value)"
        }
        Write-Output "JOURNEY_OK $($journey.Key) evidence=$($journey.Value)"
    }
}
finally {
    Pop-Location
}
