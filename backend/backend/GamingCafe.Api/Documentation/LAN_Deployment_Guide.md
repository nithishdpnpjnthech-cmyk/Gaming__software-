# LAN Deployment Guide

## 1. Multi-PC LAN Architecture
- **Backend Host**: Static LAN IP (e.g. `192.168.29.56` or `192.168.1.100`), listening on port `5022`.
- **Client Agent PCs**: Any PC connected to the local subnet.

## 2. Kestrel Server Configuration
In `backend/GamingCafe.Api/appsettings.json`:
```json
"Kestrel": {
  "Endpoints": {
    "Https": {
      "Url": "https://0.0.0.0:5022"
    }
  }
}
```
Binding to `0.0.0.0` ensures the backend listens on all network adapters.

## 3. Windows Firewall Configuration
Run PowerShell as Administrator on the Backend host PC:
```powershell
New-NetFirewallRule -DisplayName "GamingCafe Backend Port 5022" -Direction Inbound -LocalPort 5022 -Protocol TCP -Action Allow
```

## 4. Client Agent Configuration
In `GamingCafe-ClientAgent/src/GamingCafe.ClientAgent/appsettings.json`:
```json
"Backend": {
  "ApiBaseUrl": "https://192.168.29.56:5022",
  "HubUrl": "https://192.168.29.56:5022/hubs/agent",
  "ValidateCertificates": false
},
"Security": {
  "AllowInsecureDevelopmentCertificate": true
}
```
