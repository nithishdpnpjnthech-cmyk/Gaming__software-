<#
.SYNOPSIS
    Hardens local filesystem permissions for the Gaming Cafe Client Agent.
.DESCRIPTION
    Applies strict Access Control Lists (ACLs) to ensure that standard cafe users
    cannot read or tamper with Agent binaries, configuration, device identities, or credentials.
#>
[CmdletBinding()]
param(
    [string]$InstallPath = "C:\Program Files\GamingCafe\ClientAgent",
    [string]$DataPath = "C:\ProgramData\GamingCafe\ClientAgent"
)

$ErrorActionPreference = "Stop"

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "Must be run as Administrator."
    exit 1
}

Write-Host "Harden Filesystem Permissions for Gaming Cafe Client Agent" -ForegroundColor Cyan

# 1. Binary Directory: Users can Read & Execute only; Administrators & SYSTEM have Full Control
if (Test-Path $InstallPath) {
    Write-Host "[+] Hardening binary folder: $InstallPath" -ForegroundColor Yellow
    & icacls.exe "$InstallPath" /inheritance:r /grant "SYSTEM:(OI)(CI)F" "Administrators:(OI)(CI)F" "Users:(OI)(CI)RX" | Out-Null
}

# 2. Data Directory: Users have NO access; Administrators & SYSTEM have Full Control
if (-not (Test-Path $DataPath)) {
    New-Item -ItemType Directory -Path $DataPath -Force | Out-Null
}
Write-Host "[+] Hardening data & credentials folder: $DataPath" -ForegroundColor Yellow
& icacls.exe "$DataPath" /inheritance:r /grant "SYSTEM:(OI)(CI)F" "Administrators:(OI)(CI)F" | Out-Null

Write-Host "Permissions successfully applied." -ForegroundColor Green
