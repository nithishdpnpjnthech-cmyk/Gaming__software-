<#
.SYNOPSIS
    Displays status, identity, and recent logs for the Gaming Cafe Client Agent.
#>
[CmdletBinding()]
param(
    [string]$ServiceName = "GamingCafeClientAgent"
)

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$InstallDir = Split-Path -Parent $ScriptDir
$IdentityFile = Join-Path $InstallDir "data\device-identity.json"
$LogsDir = Join-Path $InstallDir "Logs"

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "  Gaming Cafe Client Agent Status Diagnostic" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

# 1. Service Status
$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($svc) {
    $statusColor = if ($svc.Status -eq 'Running') { 'Green' } else { 'Yellow' }
    Write-Host "Windows Service : Installed"
    Write-Host "Service Status  : $($svc.Status)" -ForegroundColor $statusColor
    Write-Host "Startup Type    : $($svc.StartType)"
} else {
    Write-Host "Windows Service : NOT INSTALLED" -ForegroundColor Red
}

# 2. Device Identity
if (Test-Path $IdentityFile) {
    try {
        $id = Get-Content $IdentityFile -Raw | ConvertFrom-Json
        Write-Host "`nDevice Identity Details:" -ForegroundColor Cyan
        Write-Host "  Device ID       : $($id.DeviceId)"
        Write-Host "  Device Code     : $($id.DeviceCode)" -ForegroundColor Green
        Write-Host "  Display Name    : $($id.DisplayName)"
        Write-Host "  Backend ID      : $(if ($id.BackendDeviceId) { $id.BackendDeviceId } else { 'Pending Registration' })"
        Write-Host "  Status          : $($id.RegistrationStatus)" -ForegroundColor $(if ($id.RegistrationStatus -eq 'APPROVED') { 'Green' } else { 'Yellow' })
        Write-Host "  MAC Address     : $($id.MacAddress)"
        Write-Host "  Last Updated    : $($id.LastUpdatedAtUtc)"
    } catch {
        Write-Host "  Failed to read device identity file: $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "`nDevice Identity : Not yet generated (Service will generate on first boot)" -ForegroundColor Yellow
}

# 3. Recent Log Output
if (Test-Path $LogsDir) {
    $latestLog = Get-ChildItem -Path $LogsDir -Filter "*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latestLog) {
        Write-Host "`nLatest Log Entries ($($latestLog.Name)):" -ForegroundColor Cyan
        Get-Content $latestLog.FullName -Tail 8 | ForEach-Object {
            if ($_ -match "\[CRIT\]|\[ERR\]") { Write-Host "  $_" -ForegroundColor Red }
            elseif ($_ -match "\[WARN\]") { Write-Host "  $_" -ForegroundColor Yellow }
            else { Write-Host "  $_" -ForegroundColor Gray }
        }
    }
}
Write-Host "=========================================================" -ForegroundColor Cyan
