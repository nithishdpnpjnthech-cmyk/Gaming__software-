<#
.SYNOPSIS
    Installs the Gaming Cafe Client Agent as an automatic background Windows Service.
.DESCRIPTION
    Configures the Windows Service, ensures secure data/log directories with strict ACLs,
    optionally updates server IP/Port in appsettings.json, sets recovery actions, and starts the service.
.PARAMETER ServerIp
    Optional server IP address or hostname to configure in appsettings.json.
.PARAMETER ApiPort
    Optional REST API port on the server (default: 5022).
.PARAMETER ServiceName
    Name of the Windows Service (default: GamingCafeClientAgent).
#>
[CmdletBinding()]
param(
    [string]$ServerIp,
    [int]$ApiPort = 5022,
    [string]$ServiceName = "GamingCafeClientAgent",
    [string]$DisplayName = "Gaming Cafe Client Agent",
    [string]$Description = "Maintains persistent station identity, security enrollment, health monitoring, and WebSocket communication for the Gaming Cafe Management System."
)

$ErrorActionPreference = "Stop"

# Verify Administrator elevation
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "ERROR: This script must be executed from an elevated PowerShell session (Run as Administrator)."
    exit 1
}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$InstallDir = Split-Path -Parent $ScriptDir
$ExePath = Join-Path $InstallDir "GamingCafe.ClientAgent.exe"
$ConfigFile = Join-Path $InstallDir "appsettings.json"

if (-not (Test-Path $ExePath)) {
    Write-Error "ERROR: Client Agent executable not found at '$ExePath'."
    exit 1
}

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "  Installing Gaming Cafe Client Agent (Windows Service)" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "Install Directory : $InstallDir"
Write-Host "Service Name      : $ServiceName"

# 1. Update ServerIp in appsettings.json if specified
if ($ServerIp) {
    if (Test-Path $ConfigFile) {
        Write-Host "[1/5] Updating server connection settings in appsettings.json..." -ForegroundColor Yellow
        $json = Get-Content $ConfigFile -Raw | ConvertFrom-Json
        $json.Server.ServerIp = $ServerIp
        if ($ApiPort) { $json.Server.ApiPort = $ApiPort }
        $json | ConvertTo-Json -Depth 10 | Set-Content $ConfigFile -Encoding UTF8
        Write-Host "      Configured Server IP: $ServerIp, Port: $ApiPort" -ForegroundColor Green
    }
}

# 2. Ensure data and logs directories exist with strict ACLs
Write-Host "[2/5] Creating application directories and configuring ACL permissions..." -ForegroundColor Yellow
$dataDir = Join-Path $InstallDir "data"
$logsDir = Join-Path $InstallDir "Logs"

if (-not (Test-Path $dataDir)) { New-Item -ItemType Directory -Path $dataDir -Force | Out-Null }
if (-not (Test-Path $logsDir)) { New-Item -ItemType Directory -Path $logsDir -Force | Out-Null }

# Apply strict ACLs: Administrators (Full) and SYSTEM (Full)
& icacls.exe "$dataDir" /inheritance:r /grant "SYSTEM:(OI)(CI)F" "Administrators:(OI)(CI)F" | Out-Null
& icacls.exe "$logsDir" /inheritance:r /grant "SYSTEM:(OI)(CI)F" "Administrators:(OI)(CI)F" | Out-Null

# 3. Remove existing service if already installed
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "[3/5] Existing service found. Stopping and removing previous registration..." -ForegroundColor Yellow
    if ($existingService.Status -eq 'Running') {
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 2
    }
    & sc.exe delete "$ServiceName" | Out-Null
    Start-Sleep -Seconds 1
} else {
    Write-Host "[3/5] No existing service found. Proceeding with fresh installation..." -ForegroundColor Yellow
}

# 4. Create Windows Service
Write-Host "[4/5] Registering Windows Service ($ServiceName)..." -ForegroundColor Yellow
$binPathArg = "`"$ExePath`""
$createResult = & sc.exe create "$ServiceName" binPath= "$binPathArg" start= auto DisplayName= "$DisplayName"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create Windows Service: $createResult"
    exit 1
}

# Set service description
& sc.exe description "$ServiceName" "$Description" | Out-Null

# Configure automatic recovery on failure (restart service after 5s, 10s, 30s)
& sc.exe failure "$ServiceName" reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null

# 5. Start the service
Write-Host "[5/5] Starting Windows Service..." -ForegroundColor Yellow
Start-Service -Name $ServiceName
Start-Sleep -Seconds 2

$status = Get-Service -Name $ServiceName
if ($status.Status -eq 'Running') {
    Write-Host "=========================================================" -ForegroundColor Green
    Write-Host "  Gaming Cafe Client Agent Installed and Running!" -ForegroundColor Green
    Write-Host "  Service Status: $($status.Status)" -ForegroundColor Green
    Write-Host "=========================================================" -ForegroundColor Green
} else {
    Write-Warning "Service was registered but status is: $($status.Status). Check logs at: $logsDir"
}
