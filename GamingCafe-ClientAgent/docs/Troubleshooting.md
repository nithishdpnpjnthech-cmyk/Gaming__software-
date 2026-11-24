# Gaming Cafe Client Agent — Troubleshooting Guide

## 1. Common Diagnostics and Resolutions

### Issue 1: Agent Fails to Connect with "Security validation rejected HubUrl"
- **Cause**: The `Backend:HubUrl` configured in `appsettings.json` begins with `ws://` or `http://` while `RequireWss` is `true`.
- **Resolution**: Edit `appsettings.json` and change the prefix to `wss://` or `https://`:
  ```json
  "HubUrl": "wss://admin.gamingcafe.local:5001/hubs/agent"
  ```

---

### Issue 2: TLS Certificate Validation Error (`UntrustedRoot` / `NameMismatch`)
- **Cause**: In development, the backend uses an untrusted self-signed certificate.
- **Resolution**:
  - In production: Install the cafe CA certificate into the Windows Computer Certificate Store under `Trusted Root Certification Authorities`.
  - For local dev: Provide the server certificate SHA-256 thumbprint in `appsettings.json`:
    ```json
    "ExpectedCertificateThumbprint": "A1B2C3D4E5F6..."
    ```

---

### Issue 3: Device Remains in `PendingApproval`
- **Cause**: The device has registered successfully, but an administrator has not yet clicked "Approve" in the backend web dashboard.
- **Resolution**: Open the admin dashboard, find the gaming PC matching the `DeviceId` shown in `data/device-identity.json`, and approve the device. Once approved, the backend emits `DeviceApproval` and heartbeats begin immediately.

---

### Issue 4: Corrupted Identity File
- **Cause**: Sudden PC power outage interrupted file writing.
- **Resolution**: The agent handles this automatically! It moves the corrupt file to `device-identity.json.corrupted.<timestamp>` and initializes a clean GUID identity without crashing.

---

### Issue 5: DPAPI Decryption Failure Across Machine Clones
- **Cause**: A disk image from PC-01 was cloned to PC-02, but DPAPI machine keys are unique to each physical TPM / Windows installation.
- **Resolution**: Delete `data/device-credentials.dat` on cloned PCs. The agent will cleanly request new credentials from the backend on next connection.

---

## 2. Inspecting Agent Logs

Log files are located in `logs/clientagent-YYYY-MM-DD.log` (or `%ProgramData%\GamingCafeClientAgent\logs\`).

To monitor live logs in PowerShell:
```powershell
Get-Content -Tail 50 -Wait "logs\clientagent-$(Get-Date -Format 'yyyy-MM-dd').log"
```
