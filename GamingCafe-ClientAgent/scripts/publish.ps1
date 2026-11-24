<#
.SYNOPSIS
    Publishes the Gaming Cafe Client Agent as a self-contained or framework-dependent Windows executable.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputDir = "$PSScriptRoot\..\publish"
)

$ErrorActionPreference = 'Stop'
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BaseDir = Split-Path -Parent $ScriptDir

Write-Host "=== Publishing Gaming Cafe Client Agent ($Configuration) ===" -ForegroundColor Cyan

if (Test-Path "C:\Users\Roope\AppData\Local\Microsoft\dotnet\dotnet.exe") {
    $env:DOTNET_ROOT = "C:\Users\Roope\AppData\Local\Microsoft\dotnet"
    $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
}
$env:DOTNET_CLI_HOME = "$BaseDir\.dotnet_home"
$env:APPDATA = "$BaseDir\.dotnet_home\AppData\Roaming"
$env:USERPROFILE = "$BaseDir\.dotnet_home"

$ProjectFile = "$BaseDir\src\GamingCafe.ClientAgent\GamingCafe.ClientAgent.csproj"
$ResolvedOutDir = [System.IO.Path]::GetFullPath($OutputDir)

Push-Location $BaseDir
try {
    dotnet restore "$ProjectFile" --configfile "$BaseDir\nuget.config" -r win-x64
    dotnet publish "$ProjectFile" -c $Configuration -r win-x64 --no-restore -o "$ResolvedOutDir"
    Write-Host "Published binaries available at: $ResolvedOutDir" -ForegroundColor Green
}
finally {
    Pop-Location
}
