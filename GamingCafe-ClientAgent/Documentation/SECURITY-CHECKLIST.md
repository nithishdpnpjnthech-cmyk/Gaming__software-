# Production Security Checklist & Hardening Audit

**Module:** `GamingCafe.ClientAgent`  
**Security Status:** Production-Hardened  
**Audit Date:** 2026-11-25

---

## 1. Security Controls Implemented & Verified

- [x] **Zero Plaintext Secrets on Disk:**
  - `device-credentials.dat` is encrypted using Windows Data Protection API (DPAPI).
  - DPAPI uses `DataProtectionScope.LocalMachine` paired with an application-specific cryptographic salt (`EntropySalt = Encoding.UTF8.GetBytes("GamingCafe-Agent-Entropy-V1-Salt-Key")`).
  - No database connection strings, master keys, or passwords exist in `appsettings.json`.

- [x] **File System Access Control (ACLs):**
  - Installation scripts (`Install-Agent.ps1`) automatically lock down `data/` and `Logs/`:
    - `NT AUTHORITY\SYSTEM`: FullControl
    - `BUILTIN\Administrators`: FullControl
    - Regular Windows guest accounts cannot read, modify, or tamper with station identity or logs.

- [x] **Strict Transport Layer Security (TLS):**
  - Non-WSS (`ws://`) and non-HTTPS (`http://`) connections are rejected in production when `RequireWss` or `RejectNonWss` is active.
  - Server certificate validation checks:
    - Validity date window (`NotBefore` $\le$ UTC Now $\le$ `NotAfter`).
    - Subject Alternative Name (SAN) and hostname matching (`RemoteCertificateNameMismatch`).
    - Certificate authority chain integrity (`RemoteCertificateChainErrors`).
    - Optional SHA-1 thumbprint pinning (`ExpectedCertificateThumbprint`).
  - Certificate bypass (`ValidateCertificates: false`) is strictly blocked in production.

- [x] **Log Sanitization & OWASP Compliance:**
  - `SecurityMasker` strips Bearer tokens, passwords, and sensitive keys from log output using compiled regular expressions before writing to disk.

- [x] **Message De-Duplication & Flood Protection:**
  - `MessageHandler` tracks incoming message GUIDs in an in-memory concurrent cache (up to 1,000 IDs retained for 10 minutes). Duplicate or replayed commands are rejected.

- [x] **Graceful Cooperative Cancellation:**
  - Shutdown via Ctrl+C or Windows SCM stop signals cancels the internal `CancellationToken`, closing sockets with `WebSocketCloseStatus.NormalClosure` without dumping false crash logs.

---

## 2. Remote Command Security Policy

To prevent arbitrary remote code execution (RCE) on gaming stations, the Client Agent enforces strict command boundaries:

1. **No Shell Execution:** The agent does **not** expose arbitrary bash/powershell/cmd shell execution endpoints.
2. **Explicit Command Allowlist:** Any future station operations (e.g. `LockStation`, `UnlockStation`, `RebootStation`) must be strongly typed C# DTOs registered in `MessageHandler`.
3. **Authentication Requirement:** Commands must be received over an authenticated SignalR WebSocket session presenting a valid JWT issued by the management server.
4. **Audit Logging:** Every received command logs timestamp, message ID, and execution status to rolling logs.
