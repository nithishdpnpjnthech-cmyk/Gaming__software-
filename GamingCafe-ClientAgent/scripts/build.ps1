<#
.SYNOPSIS
    Builds the Gaming Cafe Client Agent solution.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BaseDir = Split-Path -Parent $ScriptDir

Write-Host "=== Building Gaming Cafe Client Agent ($Configuration) ===" -ForegroundColor Cyan

# Set .NET 10 environment if using local SDK
if (Test-Path "C:\Users\Roope\AppData\Local\Microsoft\dotnet\dotnet.exe") {
    $env:DOTNET_ROOT = "C:\Users\Roope\AppData\Local\Microsoft\dotnet"
    $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
}
$env:DOTNET_CLI_HOME = "$BaseDir\.dotnet_home"
$env:APPDATA = "$BaseDir\.dotnet_home\AppData\Roaming"
$env:USERPROFILE = "$BaseDir\.dotnet_home"

Push-Location $BaseDir
try {
    dotnet restore "src\GamingCafe.ClientAgent\GamingCafe.ClientAgent.csproj" --configfile "$BaseDir\nuget.config"
    dotnet restore "tests\GamingCafe.ClientAgent.Tests\GamingCafe.ClientAgent.Tests.csproj" --configfile "$BaseDir\nuget.config"
    dotnet build "src\GamingCafe.ClientAgent\GamingCafe.ClientAgent.csproj" --no-restore -c $Configuration
    dotnet build "tests\GamingCafe.ClientAgent.Tests\GamingCafe.ClientAgent.Tests.csproj" --no-restore -c $Configuration
    Write-Host "Build completed successfully." -ForegroundColor Green
}
finally {
    Pop-Location
}
