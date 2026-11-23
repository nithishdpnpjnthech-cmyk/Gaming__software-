# Gaming Cafe Client Agent — Troubleshooting Guide

This guide covers common operational issues, diagnostics, and recovery procedures.

---

## 1. Quick Diagnostic Checklist

Run the built-in diagnostic script from an elevated PowerShell prompt:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\status-service.ps1
```

This inspects:
- Windows Service registration and current status
- Device identity existence and contents
- Recent tail of the agent log file

---

## 2. Common Issues & Solutions

### A. Service Fails to Start (Status: Stopped immediately)
**Symptoms**: `Start-Service "GamingCafe Client Agent"` fails or stops within 2 seconds.

**Likely Causes**:
1. **Configuration Validation Failure**:
   - Check the log file at `C:\ProgramData\GamingCafe\ClientAgent\logs\agent-YYYY-MM-DD.log`.
   - If `BackendApiBaseUrl` is set to `http://` or `SignalRHubUrl` is set to `ws://` in `Production` mode, the service fails fast by design. Change to `https://` / `wss://` or set `EnvironmentName` to `Development`.
2. **Missing Binary or Wrong Path**:
   - Verify `binPath` in `sc.exe qc "GamingCafe Client Agent"`.
   - Ensure the executable and dependent DLLs exist in the specified folder.

---

### B. SignalR Connection Fails / Reconnecting Continuously
**Symptoms**: Log shows `SignalR connection dropped. Reconnecting with exponential backoff & jitter...`

**Likely Causes**:
1. **Backend SignalR Hub Offline**: The Admin backend is restarting or offline. The Agent will automatically reconnect when the Hub comes back online.
2. **Untrusted TLS Certificate on WebSocket**: If the Admin PC uses a self-signed certificate, verify that:
   - The root certificate is trusted in Windows Certificate Store, OR
   - `TlsValidationMode` is set to `PinnedThumbprint` and `PinnedCertificateThumbprint` contains the Admin certificate's SHA-256 hash.
3. **Automatic Fallback Verification**: Note that when SignalR is unavailable, presence heartbeats automatically fall back to the HTTPS REST API without interrupting service operations.

---

### C. Corrupted Device Identity Recovery
**Symptoms**: Identity file was truncated due to sudden power loss.

**Agent Behavior**:
- The Agent automatically detects malformed or truncated `device_identity.json`.
- It moves the corrupted file to `device_identity_corrupt_YYYYMMDD_HHMMSS.bak`.
- It generates a fresh, clean Device ID and resumes registration without crashing.

**Manual Identity Reset**:
To force a Gaming PC to re-register as a completely new device:
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\stop-service.ps1
Remove-Item "C:\ProgramData\GamingCafe\ClientAgent\device_identity.json" -Force
Remove-Item "C:\ProgramData\GamingCafe\ClientAgent\cred_*.dat" -Force
powershell -ExecutionPolicy Bypass -File .\scripts\start-service.ps1
```

---

### D. Permission Denied / Access Denied
**Symptoms**: `System.UnauthorizedAccessException` when accessing `%ProgramData%\GamingCafe\ClientAgent`.

**Resolution**:
Re-run the service installation script or apply permissions as Administrator:
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-service.ps1
```

---

## 3. Log Inspection
The Client Agent writes structured, secret-sanitized logs to:
`C:\ProgramData\GamingCafe\ClientAgent\logs\agent-YYYY-MM-DD.log`

To watch live logs in PowerShell:
```powershell
Get-Content "C:\ProgramData\GamingCafe\ClientAgent\logs\agent-$(Get-Date -Format 'yyyy-MM-dd').log" -Wait -Tail 30
```
