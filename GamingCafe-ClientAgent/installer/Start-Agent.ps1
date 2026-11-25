<#
.SYNOPSIS
    Starts the Gaming Cafe Client Agent Windows Service.
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

Write-Host "Starting $ServiceName..." -ForegroundColor Cyan
$svc = Get-Service -Name $ServiceName -ErrorAction Stop

if ($svc.Status -eq 'Running') {
    Write-Host "$ServiceName is already running." -ForegroundColor Green
    return
}

Start-Service -Name $ServiceName
Start-Sleep -Seconds 2
$svc.Refresh()

Write-Host "Service Status: $($svc.Status)" -ForegroundColor Green
