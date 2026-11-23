<#
.SYNOPSIS
    Publishes a production deployment build of the Gaming Cafe Client Agent.
#>
[CmdletBinding()]
param(
    [string]$OutputDir = "$PSScriptRoot\..\publish",
    [string]$RuntimeIdentifier = "win-x64",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$scriptDir = $PSScriptRoot
$projectPath = Join-Path $scriptDir "..\src\GamingCafe.ClientAgent\GamingCafe.ClientAgent.csproj"
$nugetConfig = Join-Path $scriptDir "..\nuget.config"
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDir)

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "  Publishing Windows Client Agent Deployment Build       " -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "Target Project: $projectPath"
Write-Host "Output Dir:     $resolvedOutput"
Write-Host "Runtime:        $RuntimeIdentifier"
Write-Host "Configuration:  $Configuration"

if (Test-Path $nugetConfig) {
    dotnet restore "$projectPath" -r $RuntimeIdentifier --configfile "$nugetConfig"
}

dotnet publish "$projectPath" -c $Configuration -r $RuntimeIdentifier --self-contained false --no-restore -o "$resolvedOutput"

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nPublish completed successfully to: $resolvedOutput" -ForegroundColor Green
} else {
    Write-Error "Publish failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}
