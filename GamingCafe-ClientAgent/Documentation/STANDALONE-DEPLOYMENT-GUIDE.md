# Standalone Deployment & Windows Service Guide

**Module:** `GamingCafe.ClientAgent`  
**Target Platform:** Windows 10 / Windows 11 (64-bit)  
**Deployment Model:** Standalone Single-File Windows Service  
**Runtime Requirement:** Zero Dependencies (Self-Contained .NET 10.0 runtime bundled)

---

## 1. Architectural Model & Windows Service Lifecycle

The Client Agent is engineered as a zero-dependency, standalone Windows Service using `Microsoft.Extensions.Hosting.WindowsServices`.

### Key Characteristics
1. **Self-Contained:** Contains the entire .NET 10 runtime, base class libraries, and native binaries inside `GamingCafe.ClientAgent.exe` (71.51 MB). Client PCs require **no .NET SDK or .NET Runtime** installation.
2. **Boot-Time Execution:** Registered as `GamingCafeClientAgent` with `start= auto`. The service starts automatically when Windows boots, before any guest user logs into Windows.
3. **Automatic Crash Recovery:** Configured with Windows Service Control Manager (SCM) recovery actions:
   - 1st Failure: Restart service after 5 seconds.
   - 2nd Failure: Restart service after 10 seconds.
   - Subsequent Failures: Restart service after 30 seconds.
   - Failure Counter Reset: 86,400 seconds (24 hours).

---

## 2. Decision on Desktop Tray Application vs. Windows Service

An evaluation was performed on whether to implement a desktop notification tray application (e.g. WinForms/WPF tray icon) alongside the Windows Service.

### Evaluation Findings:
1. **Security Isolation in Public Kiosks:**
   - In a gaming cafe, PCs run with guest accounts, and kiosk software or games frequently lock or minimize the Windows shell.
   - Tray applications run in the user session context (`Session 1`), whereas Windows Services run in `Session 0` (`SYSTEM`).
   - If a tray app were used to manage connectivity or identity, a malicious guest user could kill the tray process via Task Manager or alter local files.
2. **Identity Separation & Race Conditions:**
   - Having both a service and a tray app risks running two instances of the agent concurrently, creating race conditions on `device-identity.json` and generating duplicate registrations with the backend.

### Architectural Decision:
- **The Client Agent remains strictly an unattended background Windows Service.**
- Administrative diagnosis and status checks are performed using the provided script `Get-AgentStatus.ps1`, which queries the service and inspects real-time identity and log output.

---

## 3. Directory Structure & File Permissions

### Production Installation Location:
Recommended installation paths:
- `C:\Program Files\GamingCafe-ClientAgent\` or `C:\GamingCafe\ClientAgent\`

```text
C:\GamingCafe\ClientAgent\
├── GamingCafe.ClientAgent.exe   # Standalone executable
├── appsettings.json             # Single editable configuration
├── VERSION.txt                  # Version & build info
├── README.md                    # Administrator instructions
├── data\                        # Protected DPAPI storage (device-identity.json, credentials)
├── Logs\                        # Daily rolling log files
└── Scripts\                     # PowerShell administrative scripts
```

### Access Control Lists (ACLs):
`Install-Agent.ps1` automatically enforces restricted ACLs on the installation directory:
- `NT AUTHORITY\SYSTEM`: FullControl `(OI)(CI)F`
- `BUILTIN\Administrators`: FullControl `(OI)(CI)F`
- `BUILTIN\Users`: Read & Execute only (cannot modify `appsettings.json`, cannot access `data/` or `Logs/`).

---

## 4. Multi-PC Mass Deployment Workflow

For cafe arenas with 20–200 PCs:

1. **Golden Image / Master PC Setup:**
   - Place the release folder at `C:\GamingCafe\ClientAgent\`.
   - Configure `appsettings.json` with the central server IP (e.g. `192.168.1.100`).
   - **Do NOT run the service before taking the disk image**, so `data/device-identity.json` is not yet generated.
2. **Imaging / Cloning (e.g. Clonezilla, Acronis, Deep Freeze):**
   - Deploy the disk image to all stations.
3. **First Boot on Stations:**
   - Run `Scripts\Install-Agent.ps1`.
   - On first boot, the agent reads the physical machine's MAC address and machine name, deterministically generates a unique `PC-XXXXXXXX` code, and securely registers with the central backend.
