# Gaming Café Backend API

> **High-Performance Centralized Backend & Real-Time SignalR Hub**  
> Developed with **ASP.NET Core (.NET 10)** | **Entity Framework Core 10** | **PostgreSQL 17 (pgcrypto)** | **JWT & TLS**

---

## 1. Overview

The `GamingCafe.Api` service is the central control plane for the **Gaming Café PC Management System**. It manages communication between the administrative dashboard and client agent PCs running across the local network:

- **Device Lifecycle**: Registration, approval/rejection workflows, and certificate-based enrollment.
- **Real-Time Communication**: SignalR WebSocket hub (`/hubs/agent`) receiving telemetry and streaming acknowledgments.
- **RBAC & Security**: Role-based access control, granular permission enforcement, and collision-safe JWT token issuance.
- **Telemetry & Status Tracking**: Live heartbeat resolution determining `ONLINE` vs `OFFLINE` status.
- **Connection & Security Logging**: Sanitized connection and disconnection event tracking in `device_connection_logs`.

---

## 2. Directory Structure

```text
backend/
├── GamingCafe.slnx                       # Solution configuration
├── README.md                             # Backend architecture and operations guide
├── Start-HeartbeatTest.ps1               # Automated end-to-end heartbeat test script
└── GamingCafe.Api/
    ├── Configuration/
    │   └── HeartbeatOptions.cs           # Heartbeat interval (5s) & timeout (15s) settings
    ├── Controllers/
    │   ├── AuthController.cs             # /api/auth (User login & mTLS device login)
    │   ├── DevDeviceAuthController.cs    # /api/auth/device (Development device login)
    │   ├── DeviceCredentialsController.cs# /api/devices/{id}/credentials (X.509 certs)
    │   ├── DevicesController.cs          # /api/devices (Registration, approval, rejection)
    │   └── DeviceStatusController.cs     # /api/devices/status (Live status queries)
    ├── Data/
    │   └── GamingCafeDbContext.cs        # EF Core DbContext with PostgreSQL mappings
    ├── DTO/                              # Data transfer objects for APIs and Hubs
    ├── Hubs/
    │   └── AgentHub.cs                   # SignalR WebSocket Hub (/hubs/agent)
    ├── Models/                           # PostgreSQL domain entities
    ├── Properties/
    │   └── launchSettings.json           # Development profile configurations
    ├── Services/                         # Core business and security services
    │   ├── AuthService.cs                # User credentials & password verification
    │   ├── DeviceAuthService.cs          # X.509 client certificate validator
    │   ├── DevDeviceAuthService.cs       # Development device auth service
    │   ├── DeviceCredentialService.cs     # Certificate enrollment & lifecycle
    │   ├── DeviceConnectivityResolver.cs # Online/offline calculation logic
    │   ├── DeviceStatusService.cs        # Status aggregator
    │   ├── HeartbeatService.cs           # SignalR heartbeat handler
    │   ├── JwtTokenService.cs            # JWT token generator (Admin & Device tokens)
    │   ├── RbacSeeder.cs                 # Role & permission seeder
    │   └── SuperAdminBootstrapper.cs     # Interactive first-time admin setup
    ├── appsettings.json                  # Production/base configuration (HTTPS: https://0.0.0.0:5022)
    └── Program.cs                        # Host builder, DI, auth & pipeline configuration
```

---

## 3. Technology Stack

- **Target Framework**: .NET 10.0 (`net10.0`)
- **SDK**: .NET SDK 11.0 / .NET 10 Runtime
- **Language**: C# 13
- **Web Server**: Kestrel
- **ORM**: Entity Framework Core 10.0
- **Database Driver**: Npgsql.EntityFrameworkCore.PostgreSQL 10.0
- **Database Engine**: PostgreSQL 17 (`gaming_cafe_db`)
- **Real-time Engine**: ASP.NET Core SignalR (WebSocket transport)
- **Authentication**: JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`)
- **API Spec**: Microsoft.AspNetCore.OpenApi / Microsoft.OpenApi

---

## 4. Key Endpoints

### User & Device Authentication
- `POST /api/Auth/login`: Admin/Staff username & password authentication. Returns Admin JWT containing user roles and granular permissions.
- `POST /api/Auth/device/login`: Mutual TLS (mTLS) authentication for Gaming PC client agents using enrolled X.509 certificates. Returns Device JWT.
- `POST /api/auth/device/dev-login`: Development-only authentication using backend device UUID and enrollment secret. Returns Device JWT.

### Device Lifecycle
- `POST /api/Devices/register`: Self-registration endpoint called by client agents on initial setup. Puts device in `PENDING` status and assigns backend UUID.
- `GET /api/Devices/pending`: Lists all devices waiting for administrative review (`devices.view` permission).
- `POST /api/Devices/{id}/approve`: Approves a pending device (`devices.approve` permission), updating status and synchronizing `LastStatusChangeAt` and `UpdatedAt`.
- `POST /api/Devices/{id}/reject`: Rejects a pending device (`devices.reject` permission).
- `GET /api/Devices/{id}`: Retrieves details for a specific device.

### Device Credentials (X.509 Certificates)
- `POST /api/devices/{deviceId}/credentials`: Enrolls a client certificate thumbprint for an approved device (`SUPER_ADMIN` role).
- `GET /api/devices/{deviceId}/credentials`: Lists enrolled certificates for a device.
- `DELETE /api/devices/{deviceId}/credentials/{credentialId}`: Revokes an active credential.

### Device Status & Health Monitoring
- `GET /api/devices/status`: Retrieves real-time connectivity status for all approved devices (`devices.view` permission).
- `GET /api/devices/{deviceId}/status`: Retrieves status for a single device.
- `GET /api/devices/status/summary`: Returns summary metrics (`totalDevices`, `onlineDevices`, `offlineDevices`, `pendingDevices`).

### Real-Time SignalR Agent Hub (`/hubs/agent`)
- **Route**: `/hubs/agent`
- **Transport**: WebSockets (`HttpTransportType.WebSockets`)
- **Authentication**: Device JWT Bearer passed in `?access_token=...` query string. Validated with `agent.heartbeat` policy (`token_type = device`).
- **Connection Lifecycle Logging**:
  - `OnConnectedAsync`: Records `CONNECTED` event in `device_connection_logs`.
  - `OnDisconnectedAsync`: Records `DISCONNECTED` event with sanitized disconnection reason.
- **Hub Methods**:
  - `SendHeartbeat(HeartbeatMessage message)`: Client Agent streams periodic heartbeat with machine metrics. Server validates sequence and device identity, updates database, and returns `HeartbeatAck(HeartbeatAcknowledgment)`.

---

## 5. Configuration Reference

Sample configuration in `appsettings.json` or .NET User Secrets:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=gaming_cafe_db;Username=postgres;Password=your_password;"
  },
  "Heartbeat": {
    "IntervalSeconds": 5,
    "TimeoutSeconds": 15
  },
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://0.0.0.0:5022"
      }
    }
  },
  "Jwt": {
    "Issuer": "GamingCafe.Api",
    "Audience": "GamingCafe.Client",
    "SecretKey": "YOUR_32_BYTE_BASE64_SECRET_KEY_HERE",
    "ExpiryMinutes": 60
  }
}
```

---

## 6. How to Build & Run

### 1. Database Setup
Ensure PostgreSQL 17 is running, then execute the schema script if setting up a fresh database:
```bash
psql -U postgres -d gaming_cafe_db -f ../database/schema/gaming_cafe_schema.sql
```

### 2. Configure Secrets (.NET User Secrets)
```bash
cd backend/GamingCafe.Api
dotnet user-secrets set "Jwt:SecretKey" "<BASE64_KEY_AT_LEAST_32_BYTES>"
dotnet user-secrets set "Jwt:Issuer" "GamingCafe.Api"
dotnet user-secrets set "Jwt:Audience" "GamingCafe.Client"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=gaming_cafe_db;Username=postgres;Password=secret"
```

### 3. Clean and Build
```powershell
Set-Location "C:\Users\Personal\Desktop\project\Gaming__software-\backend\GamingCafe.Api"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
dotnet clean GamingCafe.Api.csproj -c Debug
dotnet build GamingCafe.Api.csproj -c Debug
```

### 4. Mode A: Temporary Localhost HTTP Startup (Development)
For local testing without requiring a TLS certificate:

```powershell
Set-Location "C:\Users\Personal\Desktop\project\Gaming__software-\backend\GamingCafe.Api"

# Development environment
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

# Clear any Kestrel certificate environment overrides
$env:Kestrel__Endpoints__Https__Certificate__Subject = $null
$env:Kestrel__Endpoints__Https__Certificate__Store = $null
$env:Kestrel__Endpoints__Https__Certificate__Location = $null

# Bind strictly to loopback HTTP
$env:Kestrel__Endpoints__Https__Url = "http://127.0.0.1:5022"
$env:DevDeviceAuth__Enabled = "false"

# Run compiled backend
dotnet run --no-build --no-launch-profile
```

### 5. Mode B: Secure LAN HTTPS Startup (Production / Multi-PC LAN)
For secure communication across the local network with client agent PCs:

```powershell
Set-Location "C:\Users\Personal\Desktop\project\Gaming__software-\backend\GamingCafe.Api"

# Development environment
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

# Bind to all network interfaces on port 5022
$env:Kestrel__Endpoints__Https__Url = "https://0.0.0.0:5022"

# Windows Certificate Store configuration
$env:Kestrel__Endpoints__Https__Certificate__Subject = "PJN2026017"
$env:Kestrel__Endpoints__Https__Certificate__Store = "My"
$env:Kestrel__Endpoints__Https__Certificate__Location = "LocalMachine"

$env:DevDeviceAuth__Enabled = "false"

# Run compiled backend
dotnet run --no-build --no-launch-profile
```

> **Note on TLS Certificate**:
> - Development Certificate Subject: `CN=PJN2026017` (Thumbprint: `55385C595A5E3DB46415BD6299075B2C87619D48`).
> - Exported public certificate: `C:\GamingCafe\Certificates\GamingCafe-LAN-Server.cer` (Must be installed into `Trusted Root Certification Authorities` on client machines).

---

## 7. Recent Core Backend Implementations (Dev-2 Branch)

1. **Collision-Safe SignalR Device User Identifiers ([JwtTokenService.cs](file:///C:/Users/Personal/Desktop/project/Gaming__software-/backend/GamingCafe.Api/Services/JwtTokenService.cs))**:
   - Device tokens now provide a collision-safe SignalR User Identifier claim: `ClaimTypes.NameIdentifier = "device:{deviceId:D}"`.
   - Preserves `device_id` and `token_type = device` claims while maintaining strict separation from Admin user claims and a 15-minute expiration.
2. **Sanitized Disconnection Logging ([AgentHub.cs](file:///C:/Users/Personal/Desktop/project/Gaming__software-/backend/GamingCafe.Api/Hubs/AgentHub.cs))**:
   - Implemented `OnConnectedAsync` and `OnDisconnectedAsync` logging to `device_connection_logs`.
   - Raw exception details are sanitized into safe categorization codes (`ClientTimeout`, `TransportError`, `NormalClosure`) to prevent sensitive data leakage.
3. **Atomic Device Approval Synchronization ([DevicesController.cs](file:///C:/Users/Personal/Desktop/project/Gaming__software-/backend/GamingCafe.Api/Controllers/DevicesController.cs))**:
   - In `ApproveDevice`, `DeviceStatus.LastStatusChangeAt` and `DeviceStatus.UpdatedAt` are synchronized within the approval database transaction.
4. **Decoupled Kestrel LAN HTTPS Configuration ([appsettings.json](file:///C:/Users/Personal/Desktop/project/Gaming__software-/backend/GamingCafe.Api/appsettings.json))**:
   - Decoupled hardcoded machine certificate subjects from shared repository configuration, allowing flexible environment-specific overrides for both LAN HTTPS and Localhost HTTP development.
