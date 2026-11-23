# Gaming Cafe Client Agent (Phase 1)

> **Enterprise-grade Windows Background Service for Gaming PC Management**  
> Developed with **.NET 10** | **Pure SignalR over WebSocket Secure (WSS)** | **Windows DPAPI**

---

## 1. Architectural Highlights

- **100% Pure SignalR WSS Communication**: No REST APIs or HTTP polling for application communication. All enrollment, heartbeat, configuration, and revocation traffic flows through an encrypted WebSocket connection (`HttpTransportType.WebSockets`).
- **Windows Service Hosting**: Runs autonomously under `NT AUTHORITY\SYSTEM` without requiring a logged-in Windows user session.
- **Cryptographic Device Identity**: Persistent GUID v4 device identity with hardware fingerprinting and automatic corruption quarantine.
- **8-Stage Enrollment State Machine**: Full lifecycle tracking (`Unregistered`, `RegistrationPending`, `PendingApproval`, `Approved`, `Authenticated`, `Rejected`, `Revoked`, `Error`).
- **Resilient Reconnection Engine**: Exponential backoff ($2^n \times \text{delay}$) with cryptographically random jitter.
- **Hardware-Tied Credential Storage**: DPAPI encryption at rest with LocalMachine scope and an agent entropy salt.
- **Zero Attack Surface**: Outbound client only. No listening TCP ports, HTTP servers, or open endpoints.
- **Strict Privacy & Security**: Zero invasive monitoring (no screen captures, keyloggers, or remote shells).

---

## 2. Project Directory Structure

```
GamingCafe-ClientAgent/
├── src/
│   └── GamingCafe.ClientAgent/
│       ├── Authentication/          # Device credentials, secure storage, and token lifecycle
│       ├── Communication/           # SignalR connection manager, state manager, reconnection
│       ├── Configuration/           # Strongly-typed configuration, validator, and reloading
│       ├── Enrollment/              # 8-stage enrollment workflow over SignalR
│       ├── Health/                  # In-memory CPU, memory, thread, and latency metrics
│       ├── Heartbeat/               # SignalR WSS heartbeat dispatcher and ACK tracking
│       ├── Identity/                # Hardware fingerprinting, persistent GUID, atomic storage
│       ├── Logging/                 # FileLoggerProvider (10MB rotation) & secret masker
│       ├── Messaging/               # Protocol message schemas and sliding-window deduplicator
│       ├── Security/                # DPAPI protector, TLS certificate validator, secret redaction
│       ├── Services/                # ClientAgentWorker (BackgroundService)
│       ├── Program.cs               # Service host builder & DI registration
│       ├── appsettings.json         # Production configuration
│       └── appsettings.Development.json
├── tests/
│   └── GamingCafe.ClientAgent.Tests/
│       ├── ConfigurationValidationTests.cs
│       ├── CredentialProtectionTests.cs
│       ├── DeviceIdentityTests.cs
│       ├── EnrollmentServiceTests.cs
│       ├── HealthServiceTests.cs
│       ├── HeartbeatTests.cs
│       ├── MessageHandlerTests.cs
│       ├── SecurityValidationTests.cs
│       ├── MockConfigurationService.cs
│       └── Program.cs               # In-process test runner (24 passing tests)
├── docs/
│   ├── PROJECT-OVERVIEW.md          # High-level goals, scope boundaries, and tech stack
│   ├── DEPENDENCIES.md              # Framework and NuGet package manifests
│   ├── SYSTEM-ARCHITECTURE.md       # High-level architecture and Mermaid diagrams
│   ├── WEBSOCKET-COMMUNICATION.md   # SignalR hub methods, schemas, and WSS mandate
│   ├── SECURITY-DOCUMENTATION.md    # Threat model, DPAPI, and privacy guarantees
│   ├── IMPLEMENTATION-GUIDE.md      # Dependency injection and component interactions
│   ├── BACKEND-INTEGRATION.md       # ASP.NET Core server AgentHub reference code
│   ├── CONFIGURATION-GUIDE.md       # Complete appsettings.json schema reference
│   ├── INSTALLATION-AND-DEPLOYMENT.md # One-click and manual service setup guide
│   ├── TESTING-AND-VALIDATION.md    # Test architecture, execution, and outputs
│   ├── TROUBLESHOOTING.md           # Diagnostics, failure modes, and recovery
│   └── FUTURE-ENHANCEMENTS.md       # Roadmap for Phase 2, 3, and 4
├── scripts/
│   ├── build.ps1                    # Solution build script
│   ├── test.ps1                     # In-process test execution script
│   ├── publish.ps1                  # Win-x64 release publisher
│   ├── install-service.ps1          # Windows Service installer
│   ├── uninstall-service.ps1        # Windows Service uninstaller
│   ├── start-service.ps1            # Windows Service starter
│   ├── stop-service.ps1             # Windows Service stopper
│   ├── status-service.ps1           # Windows Service health inspector
│   └── Setup-SecurityPermissions.ps1 # NTFS ACL hardener for ProgramData
├── installer/
│   ├── install.ps1                  # Automated one-click deployment
│   ├── install-agent.bat            # Administrator launcher
│   └── README.md
├── nuget.config                     # Local package source configuration
├── Directory.Build.props            # Source control query bypass & strict build settings
└── README.md
```

---

## 3. Quick Start Commands

From `Gaming__software-\GamingCafe-ClientAgent`:

### Build Solution
```powershell
.\scripts\build.ps1
```

### Run Tests (24 Passing Tests)
```powershell
.\scripts\test.ps1
```

### Publish Release Binaries (win-x64)
```powershell
.\scripts\publish.ps1
```

### Install as Windows Service (Admin)
```powershell
.\installer\install.ps1
```

### Inspect Service Status
```powershell
.\scripts\status-service.ps1
```
