<#
.SYNOPSIS
    Stops the Gaming Cafe Client Agent Windows Service.
#>
[CmdletBinding()]
param(
    [string]$ServiceName = "GamingCafeClientAgent"
)

$ErrorActionPreference = "Stop"

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "ERROR: Must run as Administrator."
    exit 1
}

Write-Host "Stopping $ServiceName..." -ForegroundColor Cyan
$svc = Get-Service -Name $ServiceName -ErrorAction Stop

if ($svc.Status -eq 'Stopped') {
    Write-Host "$ServiceName is already stopped." -ForegroundColor Green
    return
}

Stop-Service -Name $ServiceName -Force
Start-Sleep -Seconds 2
$svc.Refresh()

Write-Host "Service Status: $($svc.Status)" -ForegroundColor Green
