# Gaming Cafe Management System — Client Agent (Phase 1)

[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20Service-blue.svg)](https://learn.microsoft.com/en-us/dotnet/core/extensions/windows-service)
[![Realtime](https://img.shields.io/badge/Transport-SignalR%20WSS%20%2B%20REST-orange.svg)]()
[![Security](https://img.shields.io/badge/Security-Windows%20DPAPI%20%2B%20Strict%20TLS-green.svg)]()
[![Tests](https://img.shields.io/badge/Tests-34%20Passed%20(100%25)-brightgreen.svg)]()

The **Gaming PC Client Agent** is an autonomous, resilient Windows background service running on each gaming PC in a commercial gaming cafe. It establishes cryptographic device identity, automated backend enrollment, secure credential storage via Windows DPAPI, real-time WebSocket communication over TLS (WSS) using SignalR, and presence telemetry with automatic REST fallback.

---

## 1. Project Overview & Features
- **Automatic System Startup**: Starts as a native Windows Service (`GamingCafe Client Agent`) on machine boot without requiring an interactive Windows user session.
- **Cryptographic Device Identity**: Generates and persists a cryptographically secure UUIDv4 device identifier across reboots and power outages.
- **Backend Enrollment Lifecycle**: Registers with the central Admin PC backend, awaits administrator approval, and safely exchanges credentials upon authorization.
- **Encrypted Credential Protection**: Encrypts authentication tokens at rest using Windows DPAPI (`DataProtectionScope.CurrentUser`) with application-specific context entropy and strict OS-level access control lists (`SYSTEM` and `Administrators` only).
- **Dual-Layer Communication**:
  - **SignalR WebSocket over TLS (`wss://`)**: Low-latency channel for real-time presence beacons and server administrative events.
  - **HTTPS REST API Fallback**: Resilient fallback channel when SignalR is unavailable.
- **Automatic Reconnection & Backoff**: Custom exponential backoff with full cryptographic jitter prevents thundering-herd reconnect spikes during cafe network or power recovery.
- **Sanitized Rolling File Logging**: Writes structured logs with automatic redaction of Bearer tokens, passwords, and JSON secrets, with daily rotation and 10 MB file size caps.

---

## 2. Phase 1 Scope & Exclusions

### In Scope (Phase 1)
- Windows Service hosting (`Microsoft.Extensions.Hosting.WindowsServices`)
- Device identity generation, storage, and auto-quarantine corrupt recovery
- Zero-trust HTTPS REST client with strict TLS validation
- Real-time SignalR connection manager using WebSocket over TLS (WSS)
- Safe foundational event handling (`EnrollmentStatusChanged`, `DeviceStatusAcknowledged`, `MaintenanceNotification`)
- Dual-transport heartbeat reporting (SignalR primary with automatic REST fallback)
- Windows DPAPI credential storage abstraction
- Exponential backoff with cryptographic jitter
- Automated secret masking in logs
- Management PowerShell scripts (`build`, `test`, `publish`, `install`, `uninstall`, `start`, `stop`, `status`)
- Comprehensive unit test suite (34 tests)

### Explicit Exclusions (Phase 1)
To ensure isolation and security, Phase 1 strictly excludes:
- No billing, tariffs, or payment processing
- No session timers or lock screens (WPF lock screen UI scheduled for Phase 2)
- No remote command execution, shell endpoints, or PowerShell runners
- No game process launching or game library management
- No screen captures, keystroke logging, or customer surveillance
- No direct database access (communicates exclusively via backend API and SignalR Hub)

---

## 3. Technology Stack
- **Framework**: .NET 10 (`net10.0`)
- **Language**: C# 13
- **Hosting**: `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Hosting.WindowsServices`
- **Real-time**: `Microsoft.AspNetCore.SignalR.Client` (Version 11.0.0)
- **Networking**: `Microsoft.Extensions.Http`, `SocketsHttpHandler`
- **Security**: Windows DPAPI (`System.Security.Cryptography.ProtectedData`)
- **Logging**: Microsoft structured logging with custom rolling file provider
- **Testing**: xUnit with in-process runner

---

## 4. Project Structure

```
GamingCafe-ClientAgent/
├── src/
│   └── GamingCafe.ClientAgent/
│       ├── Program.cs                  # Host builder, DI container, & Windows Service setup
│       ├── Worker.cs                   # BackgroundService orchestrating service lifecycle
│       ├── Configuration/
│       │   ├── AgentConfiguration.cs   # Strongly-typed configuration model
│       │   └── ConfigurationValidator.cs# Eager startup validator enforcing HTTPS, WSS, & intervals
│       ├── Identity/
│       │   ├── DeviceIdentityModels.cs # Identity record & interfaces
│       │   ├── DeviceIdentityService.cs# UUIDv4 generation & restart reuse
│       │   └── DeviceIdentityStore.cs  # Atomic file persistence & corrupt recovery
│       ├── Enrollment/
│       │   ├── EnrollmentState.cs      # Lifecycle state enum
│       │   ├── EnrollmentModels.cs     # Registration DTOs
│       │   └── EnrollmentService.cs    # Multi-step state machine orchestrator
│       ├── Authentication/
│       │   ├── DeviceCredentialStore.cs# Encrypted credential storage abstraction
│       │   └── DeviceAuthenticationService.cs # Token acquisition & revocation handling
│       ├── Communication/
│       │   ├── BackendApiClient.cs     # Resilient HTTPS REST client
│       │   ├── ApiException.cs         # Mapped network exception
│       │   └── ApiModels/              # ApiResponse wrapper
│       ├── Realtime/
│       │   ├── SignalRConnectionManager.cs # HubConnection lifecycle & WSS manager
│       │   ├── SignalRHubClient.cs     # Real-time message dispatch facade
│       │   ├── ConnectionState.cs      # Connection state enum
│       │   └── RealtimeEventModels.cs  # Safe foundational event models
│       ├── Heartbeat/
│       │   ├── HeartbeatService.cs     # Dual-transport presence beacon (SignalR + REST fallback)
│       │   └── HeartbeatModels.cs      # Telemetry payload & ack DTOs
│       ├── Health/
│       │   ├── AgentHealthService.cs   # Internal health telemetry tracker
│       │   └── HealthModels.cs         # Health report model
│       ├── Security/
│       │   ├── DpapiCredentialProtection.cs # Windows DPAPI encryption with context entropy
│       │   ├── SecurityConfiguration.cs# Least privilege directory ACLs
│       │   └── SecurityMasker.cs       # Automated secret & token log sanitizer
│       ├── Logging/
│       │   ├── FileLoggerProvider.cs   # Rolling file logger with daily rotation & size limits
│       │   └── LoggingConfiguration.cs # Logging setup helper
│       ├── appsettings.json            # Non-secret production configuration
│       └── appsettings.Development.json# Development configuration
├── tests/
│   └── GamingCafe.ClientAgent.Tests/   # Unit test suite (34 tests)
├── docs/
│   ├── Architecture.md                 # System diagrams & design patterns
│   ├── Security.md                     # Comprehensive threat model & DPAPI analysis
│   ├── Setup.md                        # Developer workflow & VS Code guide
│   ├── Enrollment.md                   # State machine & proposed API contracts
│   ├── Communication.md                # REST & SignalR WSS details
│   └── Troubleshooting.md              # Operational diagnostic runbook
├── scripts/
│   ├── build.ps1                       # Solution build script
│   ├── test.ps1                        # Unit test runner script
│   ├── publish.ps1                     # Production deployment publish script
│   ├── install-service.ps1             # Windows Service installer (Service: GamingCafe Client Agent)
│   ├── uninstall-service.ps1           # Service uninstaller & optional data purge
│   ├── start-service.ps1               # Service start utility
│   ├── stop-service.ps1                # Service stop utility
│   └── status-service.ps1              # Real-time status, identity, & log inspector
├── installer/
│   └── README.md                       # Fleet deployment guidelines
├── .gitignore                          # Git exclusion rules
├── Directory.Build.props               # Build props preventing OneDrive Git collisions
├── nuget.config                        # NuGet package sources config
├── README.md                           # Master documentation
└── GamingCafe.ClientAgent.sln          # Solution file
```

---

## 5. Prerequisites
- **Operating System**: Windows 10 or Windows 11 (64-bit)
- **.NET SDK**: .NET 10 SDK (Version 10.0.404 or higher)
- **Editor**: Visual Studio Code (C# Dev Kit recommended)
- **Elevation**: Administrator rights required only for Windows Service installation/uninstallation

---

## 6. Development Workflow (VS Code)

1. Open PowerShell and navigate to the project root:
   ```powershell
   cd C:\Users\Roope\OneDrive\Desktop\Gamingcafe1\Gaming__software-\GamingCafe-ClientAgent
   code .
   ```

2. Build the project:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
   ```

3. Run the unit test suite:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1
   ```

4. Run in Development Mode (Console mode):
   ```powershell
   $env:DOTNET_ENVIRONMENT = "Development"
   dotnet run --project src/GamingCafe.ClientAgent/GamingCafe.ClientAgent.csproj
   ```
   Press `Ctrl+C` to test graceful shutdown.

---

## 7. Windows Service Deployment

Open PowerShell as **Administrator**:

### Publish Deployment Package
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish.ps1
```

### Install Windows Service
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-service.ps1
```

### Start the Service
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-service.ps1
```

### Check Real-Time Status & Health
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\status-service.ps1
```

### Stop & Uninstall
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\stop-service.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\uninstall-service.ps1
```

---

## 8. Configuration Guide

Configure `src/GamingCafe.ClientAgent/appsettings.json`:

```json
{
  "GamingCafeAgent": {
    "BackendApiBaseUrl": "https://admin.cafe.lan:5001",
    "SignalRHubUrl": "https://admin.cafe.lan:5001/hubs/agent",
    "HeartbeatIntervalSeconds": 30,
    "HttpTimeoutSeconds": 10,
    "SignalRConnectionTimeoutSeconds": 15,
    "EnrollmentPollIntervalSeconds": 10,
    "MinRetryBackoffSeconds": 2,
    "MaxRetryBackoffSeconds": 60,
    "BackoffJitterFactor": 0.2,
    "AgentVersion": "1.0.0-phase1",
    "ProtocolVersion": "1.0",
    "EnvironmentName": "Production",
    "TlsValidationMode": "SystemTrust",
    "PinnedCertificateThumbprint": ""
  }
}
```

> **Security Note**: Never store credentials or tokens in `appsettings.json`. Credentials are encrypted via Windows DPAPI and stored in `%ProgramData%\GamingCafe\ClientAgent\`.

---

## 9. Future Phases Roadmap
- **Phase 2**: Fullscreen WPF Lock Screen UI, customer session start/end handling, and SignalR live push commands.
- **Phase 3**: Game launcher integration, game license allocation, and local session time warnings.
- **Phase 4**: Point-of-Sale (POS) order requests and maintenance mode locks.
