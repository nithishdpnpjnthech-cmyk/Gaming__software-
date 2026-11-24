# Gaming Cafe Client Agent — Security Hardening Documentation

## 1. Threat Mitigation Framework

| Threat Vector | Mitigation Mechanism | Verification / Enforcement |
|---|---|---|
| **Eavesdropping / MitM** | Mandatory WSS (TLS 1.2/1.3) with full certificate chain validation. | `SecurityValidationService.ValidateHubUrl` rejects `ws://` and `http://`. |
| **Rogue Backend Server** | Pinned SHA-256 certificate thumbprint validation option. | `SecurityValidationService.ValidateServerCertificate`. |
| **Credential Exfiltration from Disk** | Windows DPAPI with `LocalMachine` scope and entropy salt. | `DpapiCredentialProtection.cs` encrypts `device-credentials.dat`. |
| **Credential Leakage in Logs** | Real-time regex sanitization on all logging channels. | `SecurityMasker.SanitizeLogMessage` redacts tokens, keys, passwords. |
| **Impersonation / Device Cloning** | Hardware fingerprint hash combining CPU count, architecture, and machine name. | `DeviceIdentityService.GenerateHardwareFingerprint`. |
| **Replay Attacks** | UUIDv4 `MessageId` and UTC timestamp tracked in 10-minute sliding cache window. | `MessageHandler.TryTrackMessage`. |
| **Unauthorized Local Tampering** | NTFS ACLs locking `%ProgramData%\GamingCafeClientAgent` to SYSTEM and Administrators only. | `Setup-SecurityPermissions.ps1`. |

---

## 2. Windows DPAPI Implementation Details

The Client Agent utilizes Windows Data Protection API (`System.Security.Cryptography.ProtectedData`):
```csharp
[SupportedOSPlatform("windows")]
public sealed class DpapiCredentialProtection : ICredentialProtector
{
    private static readonly byte[] EntropySalt = Encoding.UTF8.GetBytes("GamingCafe-Agent-Entropy-V1-Salt-Key");
    private readonly DataProtectionScope _scope;

    public DpapiCredentialProtection(IConfigurationService configService, ...)
    {
        var useMachineScope = configService.CurrentConfiguration.Security.UseMachineScope;
        _scope = useMachineScope ? DataProtectionScope.LocalMachine : DataProtectionScope.CurrentUser;
    }
    // ...
}
```

- **Why LocalMachine Scope?**
  Windows Services running under `NT AUTHORITY\SYSTEM` execute in a non-interactive machine context. `LocalMachine` scope ensures credentials can be decrypted after system reboots and across user logins/logouts, while the `EntropySalt` prevents unauthorized local user processes from decrypting the file.

---

## 3. Privacy & Anti-Surveillance Guarantee

Phase 1 strictly adheres to client security and user privacy:
- **NO Keystroke Monitoring**: No global Windows hooks (`SetWindowsHookEx`) or keyboard listening.
- **NO Screen Capture**: No desktop GDI, DirectX, or Windows Media capture APIs.
- **NO Remote Code Execution**: No shell, cmd.exe, PowerShell, or process spawning endpoints.
- **NO File Exfiltration**: No arbitrary disk traversal or file upload capabilities.
