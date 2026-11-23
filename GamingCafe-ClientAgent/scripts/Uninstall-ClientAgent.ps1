<#
.SYNOPSIS
    Uninstalls the Gaming Cafe Client Agent Windows Service.
.PARAMETER ServiceName
    Name of the Windows Service. Default is 'GamingCafeClientAgent'.
.PARAMETER CleanData
    If specified, removes all persisted identity, logs, and credentials from %ProgramData%.
#>
[CmdletBinding()]
param(
    [string]$ServiceName = "GamingCafeClientAgent",
    [switch]$CleanData
)

$ErrorActionPreference = "Stop"

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "This script must be executed from an elevated PowerShell session (Run as Administrator)."
    exit 1
}

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "  Uninstalling Windows Service: $ServiceName" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -eq 'Running') {
        Write-Host "[+] Stopping service '$ServiceName'..." -ForegroundColor Yellow
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 2
    }

    Write-Host "[+] Deleting Windows Service registration..." -ForegroundColor Yellow
    & sc.exe delete $ServiceName | Out-Null
    Write-Host "[+] Service unregistered successfully." -ForegroundColor Green
} else {
    Write-Host "[i] Service '$ServiceName' was not registered." -ForegroundColor Gray
}

if ($CleanData) {
    $dataDir = Join-Path $env:ProgramData "GamingCafe\ClientAgent"
    if (Test-Path $dataDir) {
        Write-Host "[!] Cleaning up data directory at: $dataDir" -ForegroundColor Yellow
        Remove-Item -Path $dataDir -Recurse -Force
    }
}

Write-Host "Uninstallation complete." -ForegroundColor Green
