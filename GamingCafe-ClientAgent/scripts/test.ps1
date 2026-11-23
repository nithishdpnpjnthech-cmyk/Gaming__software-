<#
.SYNOPSIS
    Executes the full unit test suite for the Gaming Cafe Client Agent.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$scriptDir = $PSScriptRoot
$testProject = Join-Path $scriptDir "..\tests\GamingCafe.ClientAgent.Tests\GamingCafe.ClientAgent.Tests.csproj"

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "  Running Gaming Cafe Client Agent Test Suite            " -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

dotnet run --project "$testProject"

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nAll tests passed successfully." -ForegroundColor Green
} else {
    Write-Error "Tests failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}
