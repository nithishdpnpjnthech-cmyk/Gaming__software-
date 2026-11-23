<#
.SYNOPSIS
    Queries Windows Service status, recent log entries, and local identity file.
#>
[CmdletBinding()]
param(
    [string]$ServiceName = "GamingCafeClientAgent"
)

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "         Gaming Cafe Client Agent Status Check           " -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

# 1. Service Status
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "[*] Service Registration: INSTALLED" -ForegroundColor Green
    $service | Select-Object Name, DisplayName, Status, StartType | Format-List
} else {
    Write-Host "[!] Service Registration: NOT INSTALLED" -ForegroundColor Red
}

# 2. Identity Check
$idPath = Join-Path $env:ProgramData "GamingCafe\ClientAgent\device_identity.json"
if (Test-Path $idPath) {
    Write-Host "[*] Identity File: PRESENT at $idPath" -ForegroundColor Green
    try {
        $idContent = Get-Content $idPath | ConvertFrom-Json
        Write-Host "    - Device ID:  $($idContent.DeviceId)" -ForegroundColor White
        Write-Host "    - Hostname:   $($idContent.Hostname)" -ForegroundColor White
        Write-Host "    - Created At: $($idContent.CreatedAtUtc)" -ForegroundColor White
    } catch {
        Write-Host "    [!] Could not parse identity JSON: $_" -ForegroundColor Red
    }
} else {
    Write-Host "[i] Identity File: NOT YET INITIALIZED (will be generated on first launch)" -ForegroundColor Yellow
}

# 3. Log File Check
$logsDir = Join-Path $env:ProgramData "GamingCafe\ClientAgent\logs"
if (Test-Path $logsDir) {
    $latestLog = Get-ChildItem -Path $logsDir -Filter "*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latestLog) {
        Write-Host "`n[*] Recent Log Output ($($latestLog.Name)):" -ForegroundColor Cyan
        Get-Content $latestLog.FullName -Tail 15
    }
}
