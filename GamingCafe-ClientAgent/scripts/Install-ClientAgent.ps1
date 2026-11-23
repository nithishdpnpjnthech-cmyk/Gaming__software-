<#
.SYNOPSIS
    Installs the Gaming Cafe Client Agent as a Windows Service.
.DESCRIPTION
    Configures the Windows Service to start automatically at system boot,
    sets up failure recovery actions, and establishes strict file ACL permissions.
.PARAMETER BinaryPath
    Full path to the published GamingCafe.ClientAgent.exe. Defaults to published output.
.PARAMETER ServiceName
    Name of the Windows Service. Default is 'GamingCafeClientAgent'.
#>
[CmdletBinding()]
param(
    [string]$BinaryPath = "$PSScriptRoot\..\src\GamingCafe.ClientAgent\bin\Release\net10.0\publish\GamingCafe.ClientAgent.exe",
    [string]$ServiceName = "GamingCafeClientAgent",
    [string]$DisplayName = "Gaming Cafe PC Client Agent",
    [string]$Description = "Maintains device identity, security enrollment, and health heartbeats for the Gaming Cafe Management System."
)

$ErrorActionPreference = "Stop"

# Verify Administrator elevation
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "This script must be executed from an elevated PowerShell session (Run as Administrator)."
    exit 1
}

$resolvedBinPath = [System.IO.Path]::GetFullPath($BinaryPath)
if (-not (Test-Path $resolvedBinPath)) {
    Write-Error "Service executable not found at: '$resolvedBinPath'. Please publish the project first: dotnet publish -c Release"
    exit 1
}

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "  Installing Windows Service: $ServiceName" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

# 1. Setup secure data directory in %ProgramData%
$dataDir = Join-Path $env:ProgramData "GamingCafe\ClientAgent"
if (-not (Test-Path $dataDir)) {
    Write-Host "[+] Creating secure data directory at: $dataDir" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
}

# Set strict ACLs: Administrators (Full) and SYSTEM (Full) only
Write-Host "[+] Applying strict ACLs on $dataDir..." -ForegroundColor Yellow
& icacls.exe "$dataDir" /inheritance:r /grant "SYSTEM:(OI)(CI)F" "Administrators:(OI)(CI)F" | Out-Null

# 2. Check if service already exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "[!] Service '$ServiceName' already exists. Stopping and removing old instance..." -ForegroundColor Yellow
    & sc.exe stop $ServiceName | Out-Null
    Start-Sleep -Seconds 2
    & sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

# 3. Create Windows Service
Write-Host "[+] Registering Windows Service via sc.exe..." -ForegroundColor Green
$binPathArg = "`"$resolvedBinPath`""
$createOutput = & sc.exe create $ServiceName binPath= $binPathArg start= auto DisplayName= "`"$DisplayName`""

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create Windows Service. Output: $createOutput"
    exit $LASTEXITCODE
}

# 4. Set Service Description
& sc.exe description $ServiceName "$Description" | Out-Null

# 5. Configure Failure Recovery (Restart on 1st, 2nd, and subsequent crashes with backoff)
Write-Host "[+] Configuring automatic restart recovery actions..." -ForegroundColor Green
& sc.exe failure $ServiceName reset= 86400 actions= restart/10000/restart/30000/restart/60000 | Out-Null

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "  Service successfully installed!" -ForegroundColor Green
Write-Host "  Name:         $ServiceName" -ForegroundColor White
Write-Host "  Display Name: $DisplayName" -ForegroundColor White
Write-Host "  Binary:       $resolvedBinPath" -ForegroundColor White
Write-Host "  Start Mode:   Automatic (System Boot)" -ForegroundColor White
Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "To start the service now, run:" -ForegroundColor Yellow
Write-Host "  Start-Service -Name $ServiceName" -ForegroundColor White
