# Gaming Cafe Client Agent — WebSocket Communication Specification

## 1. WebSocket-Only Mandate

The Client Agent operates strictly over **SignalR over WebSocket Secure (WSS)**. 
- **NO REST APIs**: No HTTP endpoints are invoked for heartbeat, telemetry, status reporting, or enrollment.
- **NO HTTP POLLING**: Long-polling transport is disabled at the builder level.
- **NO SERVER-SENT EVENTS**: SSE fallback is disabled at the builder level.
- **MANDATORY WSS**: Unencrypted `ws://` and `http://` schemes are rejected in production.

```csharp
// Enforcement in SignalRConnectionManager.cs:
var builder = new HubConnectionBuilder()
    .WithUrl(config.Backend.HubUrl, options =>
    {
        // STRICT WebSockets transport only
        options.Transports = HttpTransportType.WebSockets;
        options.SkipNegotiation = false;
        options.AccessTokenProvider = () => _authService.GetAccessTokenAsync();
        // ... TLS verification callbacks ...
    })
    .WithAutomaticReconnect(_reconnectionService);
```

---

## 2. Hub Method Invocations (Client -> Hub)

### `RegisterDevice`
Dispatched on startup when `EnrollmentState == Unregistered` or `PendingApproval`.
- **Method**: `RegisterDevice`
- **Argument**: `DeviceRegistrationMessage`

```json
{
  "messageId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "version": "1.0",
  "timestampUtc": "2026-11-23T11:00:00Z",
  "deviceId": "e7b0e1a2-3c4d-4e5f-a6b7-c8d9e0f1a2b3",
  "messageType": "DeviceRegistration",
  "deviceName": "RIG-PC-01",
  "hardwareIdentifier": "sha256-hardware-hash...",
  "macAddress": "00:1A:2B:3C:4D:5E",
  "osVersion": "Microsoft Windows 11 Pro 10.0.26100",
  "clientVersion": "1.0.0"
}
```

### `SendHeartbeat`
Dispatched periodically (every 15 seconds) once in `Approved` or `Authenticated` state.
- **Method**: `SendHeartbeat`
- **Argument**: `HeartbeatMessage`

```json
{
  "messageId": "7b8c9d0e-1f2a-3b4c-5d6e-7f8a9b0c1d2e",
  "version": "1.0",
  "timestampUtc": "2026-11-23T11:00:15Z",
  "deviceId": "e7b0e1a2-3c4d-4e5f-a6b7-c8d9e0f1a2b3",
  "messageType": "Heartbeat",
  "sequenceNumber": 89,
  "agentVersion": "1.0.0",
  "upTimeSeconds": 1335.2,
  "cpuPercent": 3.8,
  "memoryUsageMb": 42.1
}
```

---

## 3. Hub Callback Events (Hub -> Client)

| Hub Event Name | Payload Class | Purpose |
|---|---|---|
| `DeviceApproval` | `DeviceApprovalMessage` | Delivers administrator decision (`IsApproved: true/false`, `AssignedZone`). |
| `DeviceAuthentication` | `DeviceAuthenticationMessage` | Delivers JWT `AccessToken`, `RefreshToken`, and `TokenExpiresAtUtc`. |
| `HeartbeatAck` | `HeartbeatAcknowledgment` | Acknowledges receipt of heartbeat; triggers RTT latency calculation. |
| `DeviceConfiguration` | `DeviceConfigurationMessage` | Dynamically updates parameters (e.g. heartbeat intervals) on the fly. |
| `DeviceRevocation` | `DeviceRevocationMessage` | Commands the agent to purge credentials and halt operations. |
| `ErrorMessage` | `ErrorMessage` | Informs agent of invalid protocol commands or rate-limiting events. |
