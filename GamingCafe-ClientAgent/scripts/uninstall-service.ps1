<#
.SYNOPSIS
    Uninstalls the Gaming Cafe Client Agent Windows Service.
.PARAMETER ServiceName
    Service registration name. Default is 'GamingCafe Client Agent'.
.PARAMETER CleanData
    If specified, purges persisted identity, logs, and credentials from %ProgramData%.
#>
[CmdletBinding()]
param(
    [string]$ServiceName = "GamingCafe Client Agent",
    [switch]$CleanData
)

$ErrorActionPreference = "Stop"

# Administrator Check
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "This script must be executed from an elevated PowerShell session (Run as Administrator)."
    exit 1
}

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "  Uninstalling Windows Service: $ServiceName             " -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -eq 'Running') {
        Write-Host "[+] Stopping service '$ServiceName' gracefully..." -ForegroundColor Yellow
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 2
    }

    Write-Host "[+] Unregistering service from Service Control Manager..." -ForegroundColor Yellow
    & sc.exe delete "`"$ServiceName`"" | Out-Null
    Write-Host "[+] Service unregistered successfully." -ForegroundColor Green
} else {
    Write-Host "[i] Service '$ServiceName' was not registered." -ForegroundColor Gray
}

if ($CleanData) {
    $dataDir = Join-Path $env:ProgramData "GamingCafe\ClientAgent"
    if (Test-Path $dataDir) {
        Write-Host "[!] Purging all client data and credentials at: $dataDir" -ForegroundColor Red
        Remove-Item -Path $dataDir -Recurse -Force
    }
} else {
    Write-Host "[i] Device identity and credentials preserved in %ProgramData%. Use -CleanData to purge." -ForegroundColor Cyan
}

Write-Host "Uninstallation complete." -ForegroundColor Green
