# Gaming Cafe Client Agent — Security Architecture & Threat Model

## Threat Model

### Attack Surface Analysis
Gaming cafes operate in an inherently semi-hostile physical environment. Customers have direct physical access to client machines, USB ports, and keyboards. Malicious or curious patrons may attempt to tamper with client software, bypass restrictions, clone identities, or eavesdrop on local traffic.

| Threat | Description | Mitigation Strategy |
| :--- | :--- | :--- |
| **Local Credential Theft** | An ordinary cafe user attempts to inspect files or memory to extract client authentication tokens. | Windows DPAPI encryption at rest with application entropy; OS ACLs restricting `%ProgramData%\GamingCafe\ClientAgent` to `SYSTEM` and `Administrators` only. Standard `Users` have zero access. |
| **Disk Cloning & Identity Replay** | Malicious actor clones an SSD to duplicate a Gaming PC's identity across multiple machines. | DPAPI binds encryption to the host OS Machine/LSA key; duplicated tokens cannot be decrypted on foreign hardware. In addition, hardware metadata digests flag clone attempts. |
| **Network Eavesdropping / MITM** | Attacker on the cafe LAN intercepts HTTP traffic between Gaming PCs and the Admin server. | Exclusive use of HTTPS and WSS with strict TLS validation. Self-signed or non-public cafe certificates must be provisioned into Windows Trusted Root Store or configured via `PinnedCertificateThumbprint`. Accept-all bypasses are strictly prohibited. |
| **Token Exfiltration via Logs** | Support logs or error dumps accidentally reveal authentication secrets. | `SecurityMasker` strips all Bearer tokens, passwords, and JSON secrets before logs are queued or flushed to disk. |
| **Process Tampering & Binary Hijacking** | Patron replaces service executable or DLLs with malicious code. | Executable installed in `C:\Program Files\GamingCafe\ClientAgent\` where `Users` have read/execute permissions only; write/modify permissions require Administrator elevation. |
| **Denial of Service via Reconnect Storm** | Cafe router reboots, causing 50+ Gaming PCs to simultaneously hammer the Admin PC. | Full cryptographic jitter applied to exponential backoff prevents synchronized reconnect spikes on both REST and SignalR. |
| **Unauthorized Remote Execution** | Attacker attempts to leverage the Client Agent to execute arbitrary commands or malware. | **Zero command execution capability**. The Agent contains no shell executors, PowerShell invocations, or process spawners. All operations are strictly outbound HTTP/WSS calls. |

---

## DPAPI Encryption Scope Rationale
Windows Data Protection API (`ProtectedData`) offers two primary scopes:

1. **`DataProtectionScope.CurrentUser`** (Recommended for dedicated service accounts / LocalService):
   - Ties encryption keys to the user profile of the executing service account.
   - Other users (including standard desktop users or other services) cannot decrypt the data even if they gain file access.
2. **`DataProtectionScope.LocalMachine`**:
   - Ties encryption keys to the Windows installation.
   - Any administrator process on the same machine can decrypt.
   - Requires rigorous file ACLs to ensure non-administrative users cannot read the ciphertext.

In Phase 1, `DpapiCredentialProtection` utilizes `DataProtectionScope.CurrentUser` combined with application-specific entropy (`"GamingCafe-Agent-v1-ContextEntropy"`). This ensures cross-application isolation and account-bound confidentiality.

---

## Filesystem Access Control Lists (ACLs)

### Data Directory (`%ProgramData%\GamingCafe\ClientAgent`)
- **Inheritance**: Disabled (`inheritance:r`).
- **SYSTEM**: `Full Control (OI)(CI)`
- **Administrators**: `Full Control (OI)(CI)`
- **Users / Everyone**: **No Access**

### Binary Directory (`%ProgramFiles%\GamingCafe\ClientAgent`)
- **SYSTEM**: `Full Control (OI)(CI)`
- **Administrators**: `Full Control (OI)(CI)`
- **Users**: `Read & Execute (OI)(CI)`

---

## TLS & WebSocket Security (WSS)
- Production mode requires HTTPS and WSS. Plain `http://` or `ws://` URLs fail startup configuration validation immediately.
- Supported validation modes:
  1. `SystemTrust` (Default): Validates certificates against the Windows Certificate Store (`X509Chain`). For internal cafe servers with private CAs, the root CA must be installed in `Local Computer -> Trusted Root Certification Authorities`.
  2. `PinnedThumbprint`: Allows administrators running self-signed certificates to pin the exact SHA-256 certificate thumbprint in `appsettings.json`.
- Disabling certificate validation (`DangerousAcceptAnyServerCertificateValidator` or returning `true` unconditionally) is **strictly disallowed in source code**.

---

## Least Privilege & Service Account
- The Agent should be installed to run under `NT AUTHORITY\LocalService` or a dedicated low-privileged service account with network access.
- It does not require local Administrator privileges to execute at runtime.

---

## Privacy & Ethical Compliance
The Gaming Cafe Client Agent strictly adheres to customer privacy standards:
- **No Keystroke Logging**
- **No Screenshot or Screen Recording**
- **No Process Spying or Window Title Scraping**
- **No Browser History or Cookie Reading**
- **No Personal File Scanning**

The Agent monitors solely:
- Service uptime and process memory footprint
- Network reachability of the Admin PC
- Device registration state
