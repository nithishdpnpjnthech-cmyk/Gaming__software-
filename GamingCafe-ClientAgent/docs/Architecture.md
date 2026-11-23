# Gaming Cafe Client Agent — Phase 1 Architecture

## Overview
The **Gaming Cafe Client Agent** is an autonomous, resilient Windows background service running on each gaming PC in a commercial gaming cafe. In Phase 1, it establishes a cryptographically secure, persistent device identity, executes the enrollment handshake with the central Admin PC backend, securely stores operational credentials at rest via Windows DPAPI, and maintains dual-layer communication (real-time SignalR WebSocket over TLS plus fallback HTTPS REST).

The Client Agent runs independently of logged-in Windows user sessions, starting automatically with Windows as a native Windows Service (`GamingCafe Client Agent`).

---

## Architectural Principles
1. **Zero Interactive Session Dependency**: Runs as a Windows Service (`Microsoft.Extensions.Hosting.WindowsServices`) without requiring user logon.
2. **Defensive Cryptographic Identity**: Every PC generates a random, cryptographically secure UUIDv4 identity persisted locally. Device ID is decoupled from spoofable hardware properties like MAC addresses.
3. **Dual-Layer Communication**:
   - **Primary**: Real-time SignalR WebSocket over TLS (`wss://`) for low-latency events and telemetry.
   - **Fallback**: HTTPS REST API (`IHttpClientFactory`) for registration, approval polling, and fallback heartbeats.
4. **Defense in Depth Credential Protection**: Combines Windows DPAPI with restrictive OS-level ACLs (`SYSTEM` and `Administrators` only).
5. **Decoupled Abstractions**: Built against interfaces (`IBackendApiClient`, `ISignalRConnectionManager`, `IDeviceIdentityService`, `IDeviceAuthenticationService`).
6. **Strict Zero-Trust TLS**: Never disables certificate validation. Supports Windows Certificate Store (`SystemTrust`) and pinned SHA-256 thumbprints.
7. **Privacy Preservation**: Strictly excludes invasive customer surveillance (no screen captures, keystrokes, active window tracking, or browsing history).

---

## Component Architecture

```
+-----------------------------------------------------------------------------------+
|                        Gaming PC (Windows Background Service)                     |
|                                                                                   |
|  +-----------------------------------------------------------------------------+  |
|  |                                  Worker                                     |  |
|  |           (BackgroundService / Windows Service Host Integration)            |  |
|  +-------------------------------------+---------------------------------------+  |
|                                        |                                          |
|       +--------------------------------+-------------------------------+          |
|       |                                |                               |          |
|  +----v--------------------+  +--------v-------------+  +--------------v-------+  |
|  |    EnrollmentService    |  |   HeartbeatService   |  |   AgentHealthService |  |
|  | (Lifecycle & Approval)  |  | (Dual-Layer Beacon)  |  | (Uptime & Telemetry) |  |
|  +------------+------------+  +--------+-------------+  +--------------+-------+  |
|               |                        |                               |          |
|               |              +---------+---------+                     |          |
|               |              |                   |                     |          |
|               |     (Primary)|         (Fallback)|                     |          |
|               |              v                   v                     |          |
|               |    +-------------------+   +--------------------+      |          |
|               |    | SignalRConnManager|   |  BackendApiClient  |      |          |
|               |    |  (WebSocket / WSS)|   |    (HTTPS REST)    |      |          |
|               |    +---------+---------+   +---------+----------+      |          |
|               |              |                       |                 |          |
+---------------|--------------|-----------------------|-----------------|----------+
                |              | (WSS)                 | (HTTPS)         |
                v              v                       v                 v
+-----------------------------------------------------------------------------------+
|                                  Admin PC Backend                                 |
|    - Device Enrollment API                  - SignalR Real-time Hub               |
|    - Device Approval Controller             - Presence & Telemetry Receiver       |
+-----------------------------------------------------------------------------------+
```

---

## Subsystem Details

### 1. Windows Service Worker (`Worker.cs`)
- Registered with the Windows Service Control Manager under the name `GamingCafe Client Agent`.
- Manages service start, stop, and clean cancellation via `stoppingToken`.
- Handles unexpected errors gracefully with exponential backoff and jitter without crashing the service.

### 2. Device Identity Subsystem (`Identity/`)
- `DeviceIdentityService`: Generates cryptographically secure UUIDv4 identifiers using `RandomNumberGenerator`.
- `DeviceIdentityStore`: Persists identity in `device_identity.json` inside `%ProgramData%\GamingCafe\ClientAgent\`. Performs atomic file replacement. Detects corrupted storage, quarantines bad files to `.bak`, and regenerates clean identity automatically.

### 3. Credential Protection & Authentication (`Security/` & `Authentication/`)
- `DpapiCredentialProtection`: Windows DPAPI (`ProtectedData.Protect` / `Unprotect`) with context entropy.
- `DeviceCredentialStore`: Encrypted credential persistence with directory ACL isolation.
- `DeviceAuthenticationService`: Manages device secret state and token revocation purging.

### 4. Real-time Communication (`Realtime/`)
- `SignalRConnectionManager`: Manages the `HubConnection` lifecycle over WSS (`wss://`).
- Automatic reconnection with exponential backoff and cryptographic jitter (`SignalRReconnectPolicy`).
- Dynamic access token injection via `AccessTokenProvider`.
- Safe foundational event dispatch (`DeviceStatusAcknowledged`, `EnrollmentStatusChanged`, `ServerHealthNotification`, `MaintenanceNotification`).

### 5. Resilient REST Communication (`Communication/`)
- `BackendApiClient`: Outbound HTTPS REST communication for registration, status polling, and heartbeat fallback.
- Uses `SocketsHttpHandler` with connection pooling and strict TLS certificate verification.

### 6. Heartbeat Subsystem (`Heartbeat/`)
- `HeartbeatService`: Dispatches presence beacons every configurable interval (default: 30s).
- Prefers real-time SignalR; automatically fails over to REST when SignalR is unavailable.
- Concurrency guard prevents overlapping heartbeat loops.

### 7. Health & Telemetry (`Health/`)
- `AgentHealthService`: Tracks service uptime, connection state, error counts, process memory, and heartbeat timestamps.
- Zero public diagnostic network listeners.
