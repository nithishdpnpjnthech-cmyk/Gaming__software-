<#
.SYNOPSIS
    Installs the Gaming Cafe Client Agent as a Windows Background Service.
    Requires Administrator privileges.
#>
[CmdletBinding()]
param(
    [string]$ServiceName = "GamingCafeClientAgent",
    [string]$DisplayName = "GamingCafe Client Agent",
    [string]$Description = "Secure background service for Gaming Cafe PC management and backend telemetry.",
    [string]$BinaryPath = "$PSScriptRoot\..\publish\GamingCafe.ClientAgent.exe"
)

$ErrorActionPreference = 'Stop'

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "Administrator privileges are required to install a Windows Service. Please run PowerShell as Administrator."
}

$ResolvedPath = [System.IO.Path]::GetFullPath($BinaryPath)
if (-not (Test-Path $ResolvedPath)) {
    Write-Error "Service executable not found at '$ResolvedPath'. Please publish the agent first using scripts\publish.ps1."
}

Write-Host "Installing Windows Service: $ServiceName..." -ForegroundColor Cyan

# Check if service already exists
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Warning "Service '$ServiceName' already exists. Stopping and removing old instance..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

# Create Windows Service
sc.exe create $ServiceName binPath= "$ResolvedPath" start= auto DisplayName= "$DisplayName"
sc.exe description $ServiceName "$Description"
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000

Write-Host "Windows Service '$ServiceName' registered successfully with auto-restart." -ForegroundColor Green
Write-Host "To start the service, run: .\scripts\start-service.ps1" -ForegroundColor Yellow
