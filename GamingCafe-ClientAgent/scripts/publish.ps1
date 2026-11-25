<#
.SYNOPSIS
    Publishes the Gaming Cafe Client Agent as an optimized, self-contained standalone Windows x64 release package.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputDir = ""
)

$ErrorActionPreference = 'Stop'
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BaseDir = Split-Path -Parent $ScriptDir
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = "$BaseDir\Release\GamingCafe-ClientAgent"
}

Write-Host "=== Publishing Gaming Cafe Client Agent Standalone Release ($Configuration) ===" -ForegroundColor Cyan

if (Test-Path "C:\Users\Roope\AppData\Local\Microsoft\dotnet\dotnet.exe") {
    $env:DOTNET_ROOT = "C:\Users\Roope\AppData\Local\Microsoft\dotnet"
    $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
}
$env:DOTNET_CLI_HOME = "$BaseDir\.dotnet_home"
$env:APPDATA = "$BaseDir\.dotnet_home\AppData\Roaming"
$env:USERPROFILE = "$BaseDir\.dotnet_home"

$ProjectFile = "$BaseDir\src\GamingCafe.ClientAgent\GamingCafe.ClientAgent.csproj"
$ResolvedOutDir = [System.IO.Path]::GetFullPath($OutputDir)

# Ensure clean output directory
if (Test-Path $ResolvedOutDir) {
    Remove-Item $ResolvedOutDir -Recurse -Force
}
New-Item -ItemType Directory -Path $ResolvedOutDir -Force | Out-Null
New-Item -ItemType Directory -Path "$ResolvedOutDir\data" -Force | Out-Null
New-Item -ItemType Directory -Path "$ResolvedOutDir\Logs" -Force | Out-Null
New-Item -ItemType Directory -Path "$ResolvedOutDir\Scripts" -Force | Out-Null

Push-Location $BaseDir
try {
    Write-Host "[1/3] Restoring dependencies..." -ForegroundColor Yellow
    dotnet restore "$ProjectFile" --configfile "$BaseDir\nuget.config" -r win-x64

    Write-Host "[2/3] Publishing standalone single-file executable..." -ForegroundColor Yellow
    dotnet publish "$ProjectFile" -c $Configuration -r win-x64 --no-restore -o "$ResolvedOutDir"

    Write-Host "[3/3] Assembling release package scripts and documentation..." -ForegroundColor Yellow
    
    # Copy scripts to release package
    Copy-Item "$BaseDir\installer\Install-Agent.ps1" "$ResolvedOutDir\Scripts\Install-Agent.ps1" -Force
    Copy-Item "$BaseDir\installer\Uninstall-Agent.ps1" "$ResolvedOutDir\Scripts\Uninstall-Agent.ps1" -Force
    Copy-Item "$BaseDir\installer\Start-Agent.ps1" "$ResolvedOutDir\Scripts\Start-Agent.ps1" -Force
    Copy-Item "$BaseDir\installer\Stop-Agent.ps1" "$ResolvedOutDir\Scripts\Stop-Agent.ps1" -Force
    Copy-Item "$BaseDir\installer\Get-AgentStatus.ps1" "$ResolvedOutDir\Scripts\Get-AgentStatus.ps1" -Force
    Copy-Item "$BaseDir\installer\Test-ServerConnection.ps1" "$ResolvedOutDir\Scripts\Test-ServerConnection.ps1" -Force

    # Create VERSION.txt
    @"
Gaming Cafe Client Agent
Version: 1.0.0
Architecture: Windows x64 (Standalone Single-File Executable)
Runtime: .NET 10.0 (Self-Contained - No local .NET runtime required)
Build Configuration: Release
Package Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
"@ | Set-Content -Path "$ResolvedOutDir\VERSION.txt" -Encoding UTF8

    $exeSize = (Get-Item "$ResolvedOutDir\GamingCafe.ClientAgent.exe").Length / 1MB
    Write-Host "=========================================================" -ForegroundColor Green
    Write-Host "  Build Succeeded!" -ForegroundColor Green
    Write-Host "  Package Location : $ResolvedOutDir" -ForegroundColor Green
    Write-Host "  Executable Size  : $([Math]::Round($exeSize, 2)) MB" -ForegroundColor Green
    Write-Host "=========================================================" -ForegroundColor Green
}
finally {
    Pop-Location
}
