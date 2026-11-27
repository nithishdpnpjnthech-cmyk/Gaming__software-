# LAN Configuration and Kestrel Binding Guide

## 1. Executive Summary
The backend Kestrel server has been configured to bind to `0.0.0.0:5022` over HTTP for isolated local development and LAN multi-PC testing.

---

## 2. Root Cause of Localhost Binding & Port Conflicts
Previously:
1. `launchSettings.json` contained an `http-local` profile specifying `"applicationUrl": "http://127.0.0.1:5022"` which forced Kestrel to bind strictly to loopback (`127.0.0.1`).
2. `appsettings.json` hardcoded a conflicting `Kestrel:Endpoints:Https:Url` pointing to `https://0.0.0.0:5022`. When launching over HTTP, Kestrel attempted to bind both HTTP and HTTPS to the exact same port `5022`, resulting in:
   ```text
   System.IO.IOException: Failed to bind to address https://0.0.0.0:5022: address already in use.
   ```

---

## 3. Implemented Fixes
1. **Removed Conflicting Kestrel Section from `appsettings.json`**:
   `appsettings.json` now acts as base configuration without locking specific transport protocols.
2. **Created Authoritative `appsettings.Development.json`**:
   Configured Development Kestrel endpoints:
   ```json
   "Kestrel": {
     "Endpoints": {
       "Http": {
         "Url": "http://0.0.0.0:5022"
       }
     }
   },
   "DevDeviceAuth": {
     "Enabled": true
   }
   ```
3. **Established Clean Launch Profiles in `launchSettings.json`**:
   - `lan-http`: Binds to `"http://0.0.0.0:5022"` under `Development` environment.
   - `https-production`: Reserved for production environments enforcing HTTPS.
4. **Updated `DevDeviceAuthController`**:
   Permits HTTP authentication over LAN specifically when `DevDeviceAuth.Enabled = true` in Development, while continuing to strictly enforce HTTPS in Production.

---

## 4. Verification Evidence

### Terminal Listening Address
Running `dotnet run --launch-profile lan-http` produces:
```text
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://0.0.0.0:5022
```

### Netstat Output
```powershell
netstat -ano | findstr :5022
```
Output:
```text
TCP    0.0.0.0:5022           0.0.0.0:0              LISTENING       7420
```

### Active Network Interfaces on this Machine
Running `Get-NetIPAddress -AddressFamily IPv4` reports:
- **Wi-Fi LAN IP**: `192.168.29.98`
- **Loopback**: `127.0.0.1`

*(Note: The other machine in your network is `192.168.29.56`, while this backend machine's Wi-Fi IP is currently `192.168.29.98`).*

### TCP Connectivity Test Result
```powershell
Test-NetConnection -ComputerName 192.168.29.98 -Port 5022
```
Output:
```text
ComputerName     : 192.168.29.98
RemoteAddress    : 192.168.29.98
RemotePort       : 5022
InterfaceAlias   : Wi-Fi
TcpTestSucceeded : True
```

---

## 5. How to Connect from Another PC on the LAN

### Step 1: Open Port 5022 in Windows Firewall
On the Backend PC, open PowerShell as Administrator and run:
```powershell
New-NetFirewallRule -DisplayName "GamingCafe Backend Port 5022 HTTP" -Direction Inbound -LocalPort 5022 -Protocol TCP -Action Allow -Profile Any
```

### Step 2: Test from the Client PC
From the other PC (e.g. `192.168.29.56`), run:
```powershell
Test-NetConnection 192.168.29.98 -Port 5022
```
Expected output:
```text
TcpTestSucceeded : True
```

### Step 3: Configure Client Agent `appsettings.json`
On the Client Agent PC:
```json
"Backend": {
  "ApiBaseUrl": "http://192.168.29.98:5022",
  "HubUrl": "http://192.168.29.98:5022/hubs/agent",
  "EnableSignalR": true,
  "RequireWss": false,
  "ValidateCertificates": false
},
"Security": {
  "RejectNonWss": false,
  "AllowInsecureDevelopmentCertificate": true
}
```

---

## 6. Exact Command to Start the Backend
```powershell
dotnet run --project backend/GamingCafe.Api --launch-profile lan-http
```
