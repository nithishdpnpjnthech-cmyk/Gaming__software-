<#
.SYNOPSIS
    Tests network reachability, TCP ports, and REST endpoints for the Gaming Cafe Server.
#>
[CmdletBinding()]
param(
    [string]$ConfigFile
)

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$InstallDir = Split-Path -Parent $ScriptDir
if (-not $ConfigFile) {
    $ConfigFile = Join-Path $InstallDir "appsettings.json"
}

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "  Testing Gaming Cafe Server Connectivity" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

if (-not (Test-Path $ConfigFile)) {
    Write-Error "Configuration file not found: $ConfigFile"
    exit 1
}

$config = Get-Content $ConfigFile -Raw | ConvertFrom-Json
$serverHost = if ($config.Server.ServerIp) { $config.Server.ServerIp } else { $config.Server.ServerHost }
if (-not $serverHost) { $serverHost = "127.0.0.1" }
$apiPort = if ($config.Server.ApiPort) { [int]$config.Server.ApiPort } else { 5022 }
$wsPort = if ($config.Server.WebSocketPort) { [int]$config.Server.WebSocketPort } else { 5001 }

Write-Host "Target Server Host : $serverHost"
Write-Host "API Port           : $apiPort"
Write-Host "WebSocket Port     : $wsPort"
Write-Host ""

# 1. ICMP Ping Test
Write-Host "[1/3] Testing ICMP Ping to $serverHost..." -NoNewline
$ping = Test-Connection -ComputerName $serverHost -Count 1 -Quiet -ErrorAction SilentlyContinue
if ($ping) {
    Write-Host " SUCCESS" -ForegroundColor Green
} else {
    Write-Host " WARNING (Ping failed or ICMP disabled on server - checking TCP ports next)" -ForegroundColor Yellow
}

# 2. TCP Port Test for REST API
Write-Host "[2/3] Testing TCP Connection to REST API Port ($serverHost:$apiPort)..." -NoNewline
try {
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $iar = $tcpClient.BeginConnect($serverHost, $apiPort, $null, $null)
    $wait = $iar.AsyncWaitHandle.WaitOne(3000, $false)
    if ($wait -and $tcpClient.Connected) {
        $tcpClient.EndConnect($iar)
        $tcpClient.Close()
        Write-Host " SUCCESS (Port Open)" -ForegroundColor Green
    } else {
        $tcpClient.Close()
        Write-Host " FAILED (Port Closed or Unreachable)" -ForegroundColor Red
    }
} catch {
    Write-Host " FAILED ($($_.Exception.Message))" -ForegroundColor Red
}

# 3. HTTP REST Verification
$protocol = if ($config.Server.UseSecureConnection) { "https" } else { "http" }
$testUrl = "$protocol://${serverHost}:${apiPort}/api/devices/register"
Write-Host "[3/3] Probing REST Endpoint: $testUrl..." -NoNewline

try {
    $req = [System.Net.HttpWebRequest]::Create($testUrl)
    $req.Method = "POST"
    $req.Timeout = 4000
    $req.ContentType = "application/json"
    $bytes = [System.Text.Encoding]::UTF8.GetBytes("{}")
    $req.ContentLength = $bytes.Length
    $stream = $req.GetRequestStream()
    $stream.Write($bytes, 0, $bytes.Length)
    $stream.Close()

    $resp = $req.GetResponse()
    $statusCode = [int]$resp.StatusCode
    $resp.Close()
    Write-Host " REACHABLE (HTTP $statusCode)" -ForegroundColor Green
} catch [System.Net.WebException] {
    if ($_.Response) {
        $httpResp = [System.Net.HttpWebResponse]$_.Response
        $code = [int]$httpResp.StatusCode
        # 400 Bad Request indicates the endpoint is active and listening!
        if ($code -eq 400 -or $code -eq 409) {
            Write-Host " REACHABLE (HTTP $code - API is active)" -ForegroundColor Green
        } else {
            Write-Host " REACHABLE (HTTP $code)" -ForegroundColor Yellow
        }
    } else {
        Write-Host " FAILED ($($_.Message))" -ForegroundColor Red
    }
} catch {
    Write-Host " FAILED ($($_.Message))" -ForegroundColor Red
}

Write-Host "=========================================================" -ForegroundColor Cyan
