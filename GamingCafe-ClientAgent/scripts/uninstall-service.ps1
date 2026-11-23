<#
.SYNOPSIS
    Uninstalls the Gaming Cafe Client Agent Windows Service.
    Requires Administrator privileges.
#>
[CmdletBinding()]
param(
    [string]$ServiceName = "GamingCafeClientAgent"
)

$ErrorActionPreference = 'Stop'

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "Administrator privileges are required to uninstall a Windows Service."
}

Write-Host "Uninstalling Windows Service: $ServiceName..." -ForegroundColor Cyan

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    if ($existing.Status -eq 'Running') {
        Write-Host "Stopping service..."
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 2
    }
    sc.exe delete $ServiceName
    Write-Host "Service '$ServiceName' deleted successfully." -ForegroundColor Green
}
else {
    Write-Host "Service '$ServiceName' is not installed." -ForegroundColor Yellow
}
