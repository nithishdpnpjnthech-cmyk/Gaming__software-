# Gaming Cafe Client Agent — Communication Architecture (REST & SignalR WSS)

## Overview
The Gaming Cafe Client Agent implements a dual-layer communication model designed for maximum reliability and real-time responsiveness:
1. **Real-time SignalR WebSocket over TLS (WSS)**: Primary channel for instantaneous server events, administrative actions, and bi-directional telemetry.
2. **Resilient HTTPS REST API**: Authoritative channel for device registration, initial enrollment handshake, status polling, and automatic fallback heartbeat transmission when SignalR is unavailable.

---

## 1. Real-time SignalR WebSocket (WSS)

### Technology Stack
- **Library**: `Microsoft.AspNetCore.SignalR.Client` (Version 11.0.0)
- **Protocol**: WebSocket over TLS (`wss://`)
- **Transport Security**: TLS 1.3 / 1.2 with strict X.509 certificate validation. Plain `ws://` connections are disallowed in production.

### Connection State Machine
The `SignalRConnectionManager` exposes explicit thread-safe connection states:

```
[Disconnected] <==== (StopAsync / Init)
       |
       | StartAsync()
       v
  [Connecting]
       |
       +---> (Success) ---> [Connected] <----+
       |                         |           | (Reconnected event)
       +---> (Failure)           | (Dropped) |
               |                 v           |
               +----------> [Reconnecting] --+
               |
               v
            [Failed]
```

- **`Disconnected`**: Idle or shut down. No network activity.
- **`Connecting`**: Initial handshake and TLS negotiation in progress.
- **`Connected`**: WebSocket active. Real-time messages flowing.
- **`Reconnecting`**: Transient network drop detected. Automatic reconnection with exponential backoff and jitter actively underway.
- **`Stopping`**: Graceful disconnect initiated during service shutdown.
- **`Failed`**: Handshake failed or credentials missing. Agent backs off and retries.

### Authentication & Handshake
SignalR connections require device authorization. The connection builder dynamically fetches the device's DPAPI-protected secret via `HttpConnectionOptions.AccessTokenProvider`:
```csharp
options.AccessTokenProvider = async () =>
{
    return await _authService.GetDeviceSecretAsync();
};
```
The token is passed securely in the WebSocket upgrade headers, eliminating URL query-string token leaks.

### Reconnection Policy (`SignalRReconnectPolicy`)
Custom retry policy applying exponential backoff with full cryptographic jitter:
$$\text{delay} = \text{clamp}\big(\min(\text{base} \cdot 2^{\text{attempt}}, \text{max}) \cdot (1 - j + 2j \cdot r),\, \text{min},\, \text{max}\big)$$
- Default parameters: `MinRetryBackoffSeconds = 2`, `MaxRetryBackoffSeconds = 60`, `BackoffJitterFactor = 0.2`.
- Prevents synchronized connection storms when a cafe router restarts.

### Safe Foundational Events
In Phase 1, the Agent registers handlers strictly for safe operational events:
- `DeviceStatusAcknowledged`: Backend acknowledgement of presence beacons.
- `EnrollmentStatusChanged`: Real-time notification if admin approves or revokes device authorization.
- `ServerHealthNotification`: Backend health notice.
- `MaintenanceNotification`: Planned cafe maintenance alerts.

> [!CAUTION]
> **No Remote Code Execution**: The Client Agent contains no handlers or mechanisms for remote PowerShell, arbitrary shell commands, script execution, or process launching.

---

## 2. HTTPS REST API

### Responsibilities
- Initial device enrollment (`POST /api/v1/devices/register`)
- Enrollment approval polling (`GET /api/v1/devices/{deviceId}/status`)
- Secondary fallback heartbeat (`POST /api/v1/devices/heartbeat`)

### Security Invariants
- Enforces HTTPS in Production mode (plain `http://` triggers startup validation failure).
- Uses `IHttpClientFactory` with pooled connections (`SocketsHttpHandler`).
- Timeout is configurable (`HttpTimeoutSeconds`, default 10s).
- All requests accept a `CancellationToken` for prompt shutdown responsiveness.

---

## 3. Heartbeat Dual-Transport Strategy

The `HeartbeatService` implements an automatic failover strategy:

```
                      +-----------------------------+
                      |   Periodic Heartbeat Trigger |
                      +--------------+--------------+
                                     |
                                     v
                        Is SignalR Connected?
                               /       \
                        (Yes) /         \ (No / Disconnected)
                             v           v
             Send via SignalR WSS        Send via HTTPS REST Fallback
                     |                               |
              (Success?)                      (Success?)
               /      \                        /      \
        (Yes) /        \ (Fails)        (Yes) /        \ (Fails)
             v          +---------> Fallback  v          v
          [PASS]                  to REST  [PASS]    Log Warning &
                                                      Apply Backoff
```

This guarantees presence monitoring remains uninterrupted even during partial server restarts or WebSocket proxy disruptions.

---

## 4. Pending Backend Team Integration Points

The Client Agent has implemented abstract contracts and configurable placeholders. The following specifications must be finalized with the backend team:

| Integration Point | Placeholder Defined | Backend Confirmation Required |
| :--- | :--- | :--- |
| **SignalR Hub Route** | `/hubs/agent` | Confirm hub route path and hub endpoint name in ASP.NET Core backend. |
| **SignalR Heartbeat Method** | `SendHeartbeat` | Confirm exact method signature and parameter names. |
| **REST Registration Route** | `/api/v1/devices/register` | Confirm route URL and JSON casing convention (camelCase vs PascalCase). |
| **REST Status Poll Route** | `/api/v1/devices/{deviceId}/status` | Confirm status query route and status string values (`PendingApproval`, `Approved`, `Rejected`, `Revoked`). |
| **Token Specification** | `deviceSecret` string | Confirm whether secret is an opaque high-entropy API key or a signed JWT. |
| **TLS Certificate Provisioning**| System Trust or Thumbprint | Confirm whether cafe LAN uses enterprise CA or self-signed cert with thumbprint pinning. |
