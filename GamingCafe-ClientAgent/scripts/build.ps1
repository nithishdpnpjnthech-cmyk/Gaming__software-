<#
.SYNOPSIS
    Builds the Gaming Cafe Client Agent solution.
#>
[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$scriptDir = $PSScriptRoot
$projectPath = Join-Path $scriptDir "..\src\GamingCafe.ClientAgent\GamingCafe.ClientAgent.csproj"
$testPath = Join-Path $scriptDir "..\tests\GamingCafe.ClientAgent.Tests\GamingCafe.ClientAgent.Tests.csproj"
$nugetConfig = Join-Path $scriptDir "..\nuget.config"

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "  Building Gaming Cafe Client Agent [$Configuration]      " -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

# Restore if config file exists
if (Test-Path $nugetConfig) {
    dotnet restore "$projectPath" --configfile "$nugetConfig"
    dotnet restore "$testPath" --configfile "$nugetConfig"
}

dotnet build "$projectPath" -c $Configuration --no-restore
dotnet build "$testPath" -c $Configuration --no-restore

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nBuild completed successfully." -ForegroundColor Green
} else {
    Write-Error "Build failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}
