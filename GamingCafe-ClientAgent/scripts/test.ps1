<#
.SYNOPSIS
    Runs the unit test suite for Gaming Cafe Client Agent.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BaseDir = Split-Path -Parent $ScriptDir

Write-Host "=== Running Gaming Cafe Client Agent Tests ===" -ForegroundColor Cyan

if (Test-Path "C:\Users\Roope\AppData\Local\Microsoft\dotnet\dotnet.exe") {
    $env:DOTNET_ROOT = "C:\Users\Roope\AppData\Local\Microsoft\dotnet"
    $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
}
$env:DOTNET_CLI_HOME = "$BaseDir\.dotnet_home"
$env:APPDATA = "$BaseDir\.dotnet_home\AppData\Roaming"
$env:USERPROFILE = "$BaseDir\.dotnet_home"

Push-Location $BaseDir
try {
    dotnet run --project "$BaseDir\tests\GamingCafe.ClientAgent.Tests"
}
finally {
    Pop-Location
}
