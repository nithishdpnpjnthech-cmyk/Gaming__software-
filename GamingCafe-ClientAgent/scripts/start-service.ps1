<#
.SYNOPSIS
    Starts the Gaming Cafe Client Agent Windows Service.
#>
[CmdletBinding()]
param(
    [string]$ServiceName = "GamingCafeClientAgent"
)

$ErrorActionPreference = 'Stop'
Write-Host "Starting service '$ServiceName'..." -ForegroundColor Cyan
Start-Service -Name $ServiceName
$svc = Get-Service -Name $ServiceName
Write-Host "Service status: $($svc.Status)" -ForegroundColor Green
