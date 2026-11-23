# Gaming Cafe Client Agent — Installation & Deployment Guide

## 1. System Requirements

- **Target Operating System**: Windows 10 (version 19041+) or Windows 11 / Windows Server 2022+ (64-bit).
- **Architecture**: `win-x64`.
- **Privileges**: Local Administrator (required for Service registration and LocalMachine DPAPI storage).
- **Network**: Outbound access to backend Hub port `5001` (HTTPS/WSS). Zero inbound ports required.

---

## 2. Automated One-Click Installation

Open PowerShell as **Administrator** and run:
```powershell
cd C:\Users\Roope\OneDrive\Desktop\Gamingcafe1\Gaming__software-\GamingCafe-ClientAgent
.\installer\install.ps1
```
*Or right-click `installer\install-agent.bat` and select **Run as Administrator**.*

The installer performs the following actions:
1. Publishes optimized `Release` binaries to `%ProgramFiles%\GamingCafe\ClientAgent`.
2. Creates data and log directories under `%ProgramData%\GamingCafeClientAgent`.
3. Sets NTFS ACL permissions securing data against non-admin users.
4. Registers the Windows Service `GamingCafeClientAgent` with `Automatic` startup.
5. Configures Windows Service failure recovery (restarts service automatically).
6. Starts the service immediately.

---

## 3. Manual Step-by-Step Installation

### Step 1: Publish Binaries
```powershell
.\scripts\publish.ps1 -Configuration Release -OutputDir "C:\Program Files\GamingCafe\ClientAgent"
```

### Step 2: Configure Hub URL
Edit `C:\Program Files\GamingCafe\ClientAgent\appsettings.json` and set your backend server URL:
```json
"Backend": {
  "HubUrl": "wss://admin.gamingcafe.local:5001/hubs/agent",
  "RequireWss": true
}
```

### Step 3: Register and Configure Windows Service
```powershell
sc.exe create GamingCafeClientAgent binPath= "C:\Program Files\GamingCafe\ClientAgent\GamingCafe.ClientAgent.exe" start= auto DisplayName= "GamingCafe Client Agent"
sc.exe description GamingCafeClientAgent "Gaming Cafe PC background client agent."
sc.exe failure GamingCafeClientAgent reset= 86400 actions= restart/5000/restart/10000/restart/30000
```

### Step 4: Start the Service
```powershell
Start-Service GamingCafeClientAgent
```

---

## 4. Operational Scripts Reference

| Task | PowerShell Script | Native Command |
|---|---|---|
| **Check Status** | `.\scripts\status-service.ps1` | `sc.exe query GamingCafeClientAgent` |
| **Start Service** | `.\scripts\start-service.ps1` | `Start-Service GamingCafeClientAgent` |
| **Stop Service** | `.\scripts\stop-service.ps1` | `Stop-Service GamingCafeClientAgent` |
| **Uninstall Service** | `.\scripts\uninstall-service.ps1` | `sc.exe delete GamingCafeClientAgent` |
| **Harden Permissions** | `.\scripts\Setup-SecurityPermissions.ps1` | `icacls.exe ...` |
