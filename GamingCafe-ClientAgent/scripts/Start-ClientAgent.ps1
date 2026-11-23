<#
.SYNOPSIS
    Starts the Gaming Cafe Client Agent Windows Service.
#>
[CmdletBinding()]
param(
    [string]$ServiceName = "GamingCafeClientAgent"
)

$ErrorActionPreference = "Stop"

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $service) {
    Write-Error "Service '$ServiceName' is not installed. Run Install-ClientAgent.ps1 first."
    exit 1
}

if ($service.Status -eq 'Running') {
    Write-Host "Service '$ServiceName' is already running." -ForegroundColor Yellow
} else {
    Write-Host "Starting service '$ServiceName'..." -ForegroundColor Green
    Start-Service -Name $ServiceName
    Start-Sleep -Seconds 1
    Get-Service -Name $ServiceName | Select-Object Name, Status, StartType | Format-Table -AutoSize
}
