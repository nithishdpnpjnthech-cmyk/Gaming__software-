<#
.SYNOPSIS
    Stops the Gaming Cafe Client Agent Windows Service.
#>
[CmdletBinding()]
param(
    [string]$ServiceName = "GamingCafeClientAgent"
)

$ErrorActionPreference = 'Stop'
Write-Host "Stopping service '$ServiceName'..." -ForegroundColor Cyan
Stop-Service -Name $ServiceName -Force
$svc = Get-Service -Name $ServiceName
Write-Host "Service status: $($svc.Status)" -ForegroundColor Yellow
