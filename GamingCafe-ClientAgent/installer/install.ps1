<#
.SYNOPSIS
    Automated one-click deployment installer for Gaming Cafe Client Agent.
    Deploys binaries to %ProgramFiles%\GamingCafe\ClientAgent and creates service.
#>
[CmdletBinding()]
param(
    [string]$InstallDir = "$env:ProgramFiles\GamingCafe\ClientAgent",
    [string]$SourcePublishDir = "$PSScriptRoot\..\publish",
    [string]$ServiceName = "GamingCafeClientAgent"
)

$ErrorActionPreference = 'Stop'

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "Administrator privileges are required for installation."
}

Write-Host "=== Installing Gaming Cafe Client Agent ===" -ForegroundColor Cyan

# 1. Publish if not published
if (-not (Test-Path "$SourcePublishDir\GamingCafe.ClientAgent.exe")) {
    Write-Host "Publishing binaries first..." -ForegroundColor Yellow
    & "$PSScriptRoot\..\scripts\publish.ps1" -OutputDir "$SourcePublishDir"
}

# 2. Stop and remove existing service if running
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Stopping existing service..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

# 3. Create target directory and copy files
Write-Host "Deploying binaries to $InstallDir..."
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}
Copy-Item -Path "$SourcePublishDir\*" -Destination $InstallDir -Recurse -Force

# 4. Create secure Data and Logs folders in %ProgramData%
$DataDir = "$env:ProgramData\GamingCafeClientAgent"
if (-not (Test-Path $DataDir)) {
    New-Item -ItemType Directory -Path "$DataDir\logs" -Force | Out-Null
    New-Item -ItemType Directory -Path "$DataDir\data" -Force | Out-Null
}

# 5. Register Windows Service
$ExePath = "$InstallDir\GamingCafe.ClientAgent.exe"
Write-Host "Registering Windows Service..."
sc.exe create $ServiceName binPath= "$ExePath" start= auto DisplayName= "GamingCafe Client Agent"
sc.exe description $ServiceName "Manages gaming cafe client PC state and maintains encrypted SignalR connection to backend."
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000

# 6. Start service
Write-Host "Starting Windows Service..."
Start-Service -Name $ServiceName

Write-Host "=== Installation Complete! Service is running. ===" -ForegroundColor Green
& "$PSScriptRoot\..\scripts\status-service.ps1"
