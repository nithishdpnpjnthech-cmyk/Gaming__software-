# Backend Logging Architecture and Operations Guide

## 1. Overview
The Gaming Cafe Backend utilizes ASP.NET Core's built-in structured logging infrastructure (`Microsoft.Extensions.Logging`) customized for high readability, operational clarity, low noise, and zero credential leakage.

---

## 2. Structured Log Format
Console output is configured via `ConsoleFormatterOptions`:
- **Timestamp**: `[yyyy-MM-dd HH:mm:ss.fff]` with millisecond UTC precision.
- **SingleLine**: `true` (guarantees clean parsing in systemd / Windows terminal / Docker logs).
- **IncludeScopes**: `true` (enriches logs with CorrelationId, HttpMethod, and RequestPath).

Example format:
```text
[2026-11-27 11:08:15.123] info: GamingCafe.Api.Middleware.RequestLoggingMiddleware[0]
      HTTP POST /api/devices/register responded 201 in 14.25 ms [CorrelationId: 0HN8J7...]
```

---

## 3. Configuration & Noise Control
Configured in `backend/GamingCafe.Api/appsettings.json`:
```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning",
    "Microsoft.AspNetCore.Hosting": "Information",
    "Microsoft.AspNetCore.Routing": "Warning",
    "Microsoft.EntityFrameworkCore": "Warning",
    "Microsoft.EntityFrameworkCore.Database.Command": "Warning",
    "Npgsql": "Warning",
    "GamingCafe.Api": "Information"
  }
}
```
- **SQL Noise Reduction**: `Microsoft.EntityFrameworkCore.Database.Command` is set to `Warning`, eliminating high-volume raw SQL queries from the terminal.
- **Application Diagnostics**: `GamingCafe.Api` set to `Information` ensures every registration, authentication, SignalR connect/disconnect, and offline timeout is immediately visible.

---

## 4. Startup Summary Banner
Upon launch, the backend outputs an executive configuration summary:
```text
================================================================================
  GAMING CAFE MANAGEMENT SYSTEM - BACKEND API (.NET 10)
  Version:      1.0.0
  Environment:  Development
  PostgreSQL:   Host=localhost, Port=5432, Database=gamingcafe1
  SignalR Hub:  /hubs/agent (Auth: agent.heartbeat)
  Heartbeat:    Interval=15s, Timeout=45s
  Services:     DeviceOfflineMonitorService (active)
================================================================================
PostgreSQL Connection: SUCCESS (Database: gamingcafe1)
```

---

## 5. Security & Sensitive Data Protection
Under no circumstances are any of the following values logged:
- Database passwords
- Device enrollment secrets (`enrollmentSecret`)
- Secret hashes (`secret_hash`)
- JWT access tokens
- HTTP `Authorization` headers
- Request body credentials

Only identifiers (`BackendDeviceId`, `DeviceCode`, `LocalDeviceId`) and sanitized reasons are emitted.
