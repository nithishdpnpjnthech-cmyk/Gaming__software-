<#
.SYNOPSIS
    Installs the Gaming Cafe Client Agent as a Windows Service.
.DESCRIPTION
    Configures the Windows Service to run continuously in the background,
    starting automatically at system boot, with recovery actions and restricted file ACLs.
.PARAMETER BinaryPath
    Full path to the published GamingCafe.ClientAgent.exe. Defaults to published output.
.PARAMETER ServiceName
    Service registration name. Default is 'GamingCafe Client Agent'.
#>
[CmdletBinding()]
param(
    [string]$BinaryPath = "$PSScriptRoot\..\publish\GamingCafe.ClientAgent.exe",
    [string]$ServiceName = "GamingCafe Client Agent",
    [string]$DisplayName = "Gaming Cafe PC Client Agent",
    [string]$Description = "Maintains persistent device identity, secure enrollment, real-time WSS connection, and presence telemetry for the Gaming Cafe Management System."
)

$ErrorActionPreference = "Stop"

# 1. Administrator Check
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "This script must be executed from an elevated PowerShell session (Run as Administrator)."
    exit 1
}

$resolvedBinPath = [System.IO.Path]::GetFullPath($BinaryPath)
if (-not (Test-Path $resolvedBinPath)) {
    Write-Error "Service executable not found at: '$resolvedBinPath'. Please run .\publish.ps1 first."
    exit 1
}

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "  Installing Windows Service: $ServiceName               " -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

# 2. Setup secure data directory in %ProgramData%
$dataDir = Join-Path $env:ProgramData "GamingCafe\ClientAgent"
if (-not (Test-Path $dataDir)) {
    Write-Host "[+] Creating secure data directory: $dataDir" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
}

# Apply strict ACLs: Administrators & SYSTEM (Full Control), Users (No Access)
Write-Host "[+] Hardening filesystem permissions on data directory..." -ForegroundColor Yellow
& icacls.exe "$dataDir" /inheritance:r /grant "SYSTEM:(OI)(CI)F" "Administrators:(OI)(CI)F" | Out-Null

# 3. Check for existing service installation (Safe upgrade behavior)
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "[!] Service '$ServiceName' is currently installed (Status: $($existingService.Status))." -ForegroundColor Yellow
    Write-Host "[!] Performing safe upgrade: stopping existing instance before updating..." -ForegroundColor Yellow
    if ($existingService.Status -eq 'Running') {
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 2
    }
    & sc.exe delete "`"$ServiceName`"" | Out-Null
    Start-Sleep -Seconds 2
}

# 4. Register Windows Service
Write-Host "[+] Registering Windows Service via sc.exe..." -ForegroundColor Green
$binPathArg = "`"$resolvedBinPath`""
$createOutput = & sc.exe create "`"$ServiceName`"" binPath= $binPathArg start= auto DisplayName= "`"$DisplayName`""

if ($LASTEXITCODE -ne 0) {
    Write-Error "sc.exe create failed with exit code $LASTEXITCODE. Output: $createOutput"
    exit $LASTEXITCODE
}

# 5. Configure description and crash recovery actions
& sc.exe description "`"$ServiceName`"" "$Description" | Out-Null
& sc.exe failure "`"$ServiceName`"" reset= 86400 actions= restart/10000/restart/30000/restart/60000 | Out-Null

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "  Service successfully registered!" -ForegroundColor Green
Write-Host "  Name:         $ServiceName" -ForegroundColor White
Write-Host "  Display Name: $DisplayName" -ForegroundColor White
Write-Host "  Binary Path:  $resolvedBinPath" -ForegroundColor White
Write-Host "  Start Mode:   Automatic (System Boot)" -ForegroundColor White
Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "To start the service, execute:" -ForegroundColor Yellow
Write-Host "  powershell -ExecutionPolicy Bypass -File .\start-service.ps1" -ForegroundColor White
