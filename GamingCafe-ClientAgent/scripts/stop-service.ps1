<#
.SYNOPSIS
    Stops the Gaming Cafe Client Agent Windows Service.
#>
[CmdletBinding()]
param(
    [string]$ServiceName = "GamingCafe Client Agent"
)

$ErrorActionPreference = "Stop"

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $service) {
    Write-Warning "Service '$ServiceName' is not installed."
    exit 0
}

if ($service.Status -eq 'Stopped') {
    Write-Host "Service '$ServiceName' is already stopped." -ForegroundColor Yellow
} else {
    Write-Host "Stopping service '$ServiceName' gracefully..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force
    Start-Sleep -Seconds 1
    Get-Service -Name $ServiceName | Select-Object Name, Status, StartType | Format-Table -AutoSize
}
