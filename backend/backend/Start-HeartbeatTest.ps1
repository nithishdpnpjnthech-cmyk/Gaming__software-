
$ErrorActionPreference = "Stop"

Set-Location $PSScriptRoot

Write-Host "`n=== GamingCafe Heartbeat Test Setup ===" -ForegroundColor Cyan

# 1. Configuration

$port = 5023
$baseUrl = "http://localhost:$port"
$deviceId = "59da1883-5683-4655-8dc2-32a5f178ce42"

$apiDirectory = (Resolve-Path ".\GamingCafe.Api").Path

$apiExe = Join-Path $apiDirectory `
    "bin\Debug\net10.0\GamingCafe.Api.exe"

if (-not (Test-Path $apiExe)) {
    throw "Backend executable not found."
}

# 2. Stop only the verified backend on port 5023

$listeners = Get-NetTCPConnection `
    -LocalPort $port `
    -State Listen `
    -ErrorAction SilentlyContinue

$processIds = @(
    $listeners |
        Select-Object -ExpandProperty OwningProcess -Unique
)

foreach ($processId in $processIds) {

    $process = Get-Process -Id $processId

    if ($process.Path -ne $apiExe) {
        throw "Port $port belongs to an unexpected process. No process stopped."
    }

    Write-Host "Stopping test backend PID $processId"

    Stop-Process -Id $processId

    Wait-Process -Id $processId `
        -Timeout 15 `
        -ErrorAction SilentlyContinue
}

Start-Sleep -Seconds 3

if (Get-NetTCPConnection -LocalPort $port `
    -State Listen -ErrorAction SilentlyContinue) {
    throw "Port $port is still occupied."
}

# 3. Generate temporary device credentials

$secretBytes = [byte[]]::new(32)

$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()

try {
    $rng.GetBytes($secretBytes)
}
finally {
    $rng.Dispose()
}

$deviceSecret = [Convert]::ToBase64String($secretBytes)

$sha256 = [System.Security.Cryptography.SHA256]::Create()

try {
    $hashBytes = $sha256.ComputeHash(
        [System.Text.Encoding]::UTF8.GetBytes($deviceSecret)
    )
}
finally {
    $sha256.Dispose()
}

$deviceSecretHash = [BitConverter]::ToString(
    $hashBytes
).Replace("-", "")

# 4. Configure Kestrel

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = $baseUrl

$env:DevDeviceAuth__Enabled = "true"

$configKey = "DevDeviceAuth__Devices__${deviceId}__SecretHash"

Set-Item -Path "Env:$configKey" -Value $deviceSecretHash

# 5. Start backend

Write-Host "`nStarting Kestrel on port $port..."

$backendProcess = Start-Process `
    -FilePath $apiExe `
    -WorkingDirectory $apiDirectory `
    -PassThru

$ready = $false

for ($i = 0; $i -lt 30; $i++) {

    Start-Sleep -Seconds 1

    $backendProcess.Refresh()

    if ($backendProcess.HasExited) {
        throw "Kestrel exited during startup."
    }

    $listeners = Get-NetTCPConnection `
        -LocalPort $port `
        -State Listen `
        -ErrorAction SilentlyContinue

    if (@($listeners.OwningProcess) -contains $backendProcess.Id) {
        $ready = $true
        break
    }
}

if (-not $ready) {
    throw "Kestrel did not start within 30 seconds."
}

Write-Host "[PASS] Kestrel running: $baseUrl" -ForegroundColor Green

# 6. Verify device JWT authentication

$loginBody = @{
    deviceId = $deviceId
    enrollmentSecret = $deviceSecret
} | ConvertTo-Json

$deviceLogin = Invoke-RestMethod `
    -Uri "$baseUrl/api/auth/device/dev-login" `
    -Method Post `
    -ContentType "application/json" `
    -Body $loginBody `
    -TimeoutSec 15

$deviceToken = $deviceLogin.accessToken

if (-not $deviceToken) {
    throw "Device JWT authentication failed."
}

if ([string]$deviceLogin.deviceId -ne $deviceId) {
    throw "Authenticated device ID does not match."
}

Write-Host "[PASS] Device JWT authentication" -ForegroundColor Green

# 7. Verify SignalR negotiation

$negotiation = Invoke-RestMethod `
    -Uri "$baseUrl/hubs/agent/negotiate?negotiateVersion=1" `
    -Method Post `
    -Headers @{
        Authorization = "Bearer $deviceToken"
    } `
    -TimeoutSec 15

if (-not $negotiation.connectionToken) {
    throw "SignalR negotiation failed."
}

Write-Host "[PASS] SignalR negotiation" -ForegroundColor Green

# 8. Configure test client

$env:GAMINGCAFE_TEST_SERVER_URL = $baseUrl
$env:GAMINGCAFE_TEST_DEVICE_ID = $deviceId
$env:GAMINGCAFE_TEST_DEVICE_SECRET = $deviceSecret

# 9. Display verification summary

Write-Host "`n=== Verification Summary ===" -ForegroundColor Cyan

[PSCustomObject]@{
    BackendProcessId = $backendProcess.Id
    BackendRunning = $true
    ServerUrl = $baseUrl
    DeviceId = $deviceId
    SecretAvailable = [bool]$deviceSecret
    SecretLength = $deviceSecret.Length
    DeviceLoginSuccessful = [bool]$deviceToken
    TokenExpiresAt = $deviceLogin.expiresAt
    NegotiationSuccessful = [bool]$negotiation.connectionToken
    AvailableTransports = (
        $negotiation.availableTransports.transport -join ", "
    )
} | Format-List

Remove-Variable loginBody,deviceToken,deviceLogin `
    -ErrorAction SilentlyContinue

Write-Host "[READY] SignalR test client configuration completed." -ForegroundColor Green
