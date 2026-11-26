# Gaming Cafe Backend API

> **High-Performance Centralized Backend & Real-Time SignalR Hub**  
> Developed with **ASP.NET Core (.NET 10)** | **Entity Framework Core 10** | **PostgreSQL (pgcrypto)** | **JWT & mTLS**

---

## 1. Overview

The `GamingCafe.Api` service is the central control plane for the Gaming Cafe PC Management System. It coordinates:
- **Device Lifecycle**: Registration, approval/rejection workflows, and certificate-based enrollment.
- **Real-Time Communication**: SignalR WebSocket hub (`/hubs/agent`) receiving telemetry and streaming acknowledgments.
- **RBAC & Security**: Role-based access control, granular permission enforcement, and JWT token issuance.
- **Telemetry & Status Tracking**: Live heartbeat resolution determining `ONLINE` vs `OFFLINE` status.
- **Audit & Security Logging**: Structured audit events for administrative actions and security auditing.

---

## 2. Directory Structure

```
backend/
├── GamingCafe.slnx                  # Solution configuration
├── Start-HeartbeatTest.ps1          # E2E test script for device auth & heartbeat
└── GamingCafe.Api/
    ├── Configuration/
    │   └── HeartbeatOptions.cs      # Heartbeat interval & timeout settings
    ├── Controllers/
    │   ├── AuthController.cs        # /api/auth (User login & mTLS device login)
    │   ├── DevDeviceAuthController.cs# /api/auth/device (Development loopback login)
    │   ├── DeviceCredentialsController.cs # /api/devices/{id}/credentials (X.509 certs)
    │   ├── DevicesController.cs     # /api/devices (Registration & approval)
    │   └── DeviceStatusController.cs# /api/devices/status (Live status queries)
    ├── Data/
    │   └── GamingCafeDbContext.cs   # EF Core DbContext with PostgreSQL mappings
    ├── DTO/                         # Data transfer objects for APIs and Hubs
    ├── Hubs/
    │   └── AgentHub.cs              # SignalR WebSocket Hub (/hubs/agent)
    ├── Models/                      # PostgreSQL domain entities
    ├── Services/                    # Core business and security services
    │   ├── AuthService.cs           # User credentials & password verification
    │   ├── DeviceAuthService.cs     # X.509 client certificate validator
    │   ├── DevDeviceAuthService.cs  # Development device auth service
    │   ├── DeviceCredentialService.cs# Certificate enrollment & lifecycle
    │   ├── DeviceConnectivityResolver.cs # Online/offline calculation logic
    │   ├── DeviceStatusService.cs   # Status aggregator
    │   ├── HeartbeatService.cs      # SignalR heartbeat handler
    │   ├── JwtTokenService.cs       # JWT token generator
    │   ├── RbacSeeder.cs            # Role & permission seeder
    │   └── SuperAdminBootstrapper.cs# Interactive first-time admin setup
    ├── appsettings.json             # Configuration file
    └── Program.cs                   # Host builder, DI, auth & pipeline configuration
```

---

## 3. Technology Stack

- **Target Framework**: .NET 10.0
- **Language**: C# 13
- **ORM**: Entity Framework Core 10.0
- **Database Driver**: Npgsql.EntityFrameworkCore.PostgreSQL 10.0
- **Real-time Engine**: ASP.NET Core SignalR
- **Authentication**: JWT Bearer + X.509 Client Certificates (mTLS)
- **API Spec**: Microsoft.AspNetCore.OpenApi / Microsoft.OpenApi

---

## 4. Key Endpoints

### User & Device Authentication
- `POST /api/Auth/login`: Admin/Staff username & password authentication. Returns JWT containing user roles and granular permissions.
- `POST /api/Auth/device/login`: Mutual TLS (mTLS) authentication for Gaming PC client agents using enrolled X.509 certificates. Returns device JWT.
- `POST /api/auth/device/dev-login`: Development-only authentication for local testing over loopback.

### Device Lifecycle
- `POST /api/Devices/register`: Self-registration endpoint called by client agents on initial setup. Puts device in `PENDING` status.
- `GET /api/Devices/pending`: Lists all devices waiting for administrative review (`devices.view` permission).
- `POST /api/Devices/{id}/approve`: Approves a pending device (`devices.approve` permission).
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
- **Transport**: WebSockets (`HttpTransportType.WebSockets`)
- **Authentication**: JWT Bearer passed in `?access_token=...` query string. Validated with `agent.heartbeat` policy.
- **Hub Methods**:
  - `SendHeartbeat(HeartbeatMessage message)`: Agent streams periodic heartbeat with machine metrics. Server validates sequence and device identity, updates database, and returns `HeartbeatAck(HeartbeatAcknowledgment)`.

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
  "Jwt": {
    "Issuer": "GamingCafe.Api",
    "Audience": "GamingCafe.Client",
    "SecretKey": "YOUR_32_BYTE_BASE64_SECRET_KEY_HERE",
    "ExpiryMinutes": 60
  }
}
```

---

## 6. How to Run & Bootstrap

### 1. Database Setup
Ensure PostgreSQL is running, then execute the schema script:
```bash
psql -U postgres -d gaming_cafe_db -f ../database/schema/gaming_cafe_schema.sql
```

### 2. Configure Secrets
```bash
dotnet user-secrets set "Jwt:SecretKey" "<BASE64_KEY_AT_LEAST_32_BYTES>"
dotnet user-secrets set "Jwt:Issuer" "GamingCafe.Api"
dotnet user-secrets set "Jwt:Audience" "GamingCafe.Client"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=gaming_cafe_db;Username=postgres;Password=secret"
```

### 3. Bootstrap First Super Admin
When initializing a clean database, run the one-time interactive bootstrap:
```bash
dotnet run -- --bootstrap-super-admin
```

### 4. Start the Backend API
```bash
dotnet run
```
The server will verify the PostgreSQL connection on startup and begin listening on Kestrel.

### 5. Run the Automated Heartbeat Test
```powershell
.\Start-HeartbeatTest.ps1
```
