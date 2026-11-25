<#
.SYNOPSIS
    Uninstalls the Gaming Cafe Client Agent Windows Service.
.PARAMETER ServiceName
    Name of the service to uninstall (default: GamingCafeClientAgent).
.PARAMETER KeepData
    Switch to preserve local device identity and logs. Default is to preserve.
#>
[CmdletBinding()]
param(
    [string]$ServiceName = "GamingCafeClientAgent",
    [switch]$KeepData = $true
)

$ErrorActionPreference = "Stop"

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "ERROR: This script must be executed from an elevated PowerShell session (Run as Administrator)."
    exit 1
}

Write-Host "=== Uninstalling Gaming Cafe Client Agent ($ServiceName) ===" -ForegroundColor Cyan

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -eq 'Running') {
        Write-Host "Stopping service..." -ForegroundColor Yellow
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 2
    }

    Write-Host "Removing service registration..." -ForegroundColor Yellow
    & sc.exe delete "$ServiceName" | Out-Null
    Start-Sleep -Seconds 1
    Write-Host "Windows Service successfully uninstalled." -ForegroundColor Green
} else {
    Write-Host "Service '$ServiceName' is not installed." -ForegroundColor Yellow
}

if ($KeepData) {
    Write-Host "Device identity and logs have been preserved in the installation folder." -ForegroundColor Green
}
