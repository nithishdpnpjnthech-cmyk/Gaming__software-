# Proposed SignalR & Heartbeat Contract Specification

**Project:** Gaming Cafe Management System  
**Module:** Gaming Cafe Client Agent ↔ Backend SignalR Hub  
**Author:** Gaming Cafe Client Agent Team  
**Status:** **Proposed / Awaiting Backend Implementation**  
**Default Client Mode:** **Dormant (`EnableSignalR: false`)**

---

## 1. Executive Summary & Objective

The Gaming Cafe Client Agent is architected to communicate with the central Gaming Cafe management system via **SignalR over WebSocket Secure (WSS)** for low-latency duplex telemetry, real-time command dispatch, and persistent state synchronization.

As verified during architectural auditing of `backend/GamingCafe.Api`, the backend currently implements RESTful device registration (`POST /api/devices/register`) and admin approval APIs (`/api/devices/pending`, `/api/devices/{id}/approve`), but **does not yet expose an active SignalR Hub or WebSocket endpoint**.

To prevent futile connection attempts, infinite socket retry loops, and unhandled connection exceptions, the Client Agent operates with **`EnableSignalR: false`** by default. All client-side SignalR infrastructure, WSS protocol handlers, heartbeat services, and security callbacks are implemented, verified, and dormant until the backend teammate provides the server-side SignalR Hub implementation according to this specification.

---

## 2. Proposed Hub Endpoint & Transport

### 2.1 Transport Protocol
- **Transport Type:** Strictly `HttpTransportType.WebSockets` (no LongPolling or Server-Sent Events fallback).
- **Transport Security:** WSS (`wss://`) strictly mandatory in production.
- **Initial Handshake:** HTTPS (`https://`) initial upgrade handshake.

### 2.2 Endpoint Routes
- **Production URL:** `wss://admin.gamingcafe.local:7267/hubs/agent`
- **Development URL:** `wss://localhost:7267/hubs/agent` (or `http://localhost:5022/hubs/agent` for non-TLS local dev)

---

## 3. Authentication & Authorization Contract

### 3.1 Handshake Authentication
SignalR connections must be authenticated using **JWT Bearer Tokens**.
- The client provides the token dynamically via `options.AccessTokenProvider`:
  ```csharp
  options.AccessTokenProvider = () => _authService.GetAccessTokenAsync();
  ```
- During the initial WebSocket upgrade HTTP negotiate request, the token is passed in the standard `Authorization: Bearer <token>` header (or as `access_token` query string parameter for direct WebSocket upgrades per ASP.NET Core SignalR standard).

### 3.2 Required JWT Claims
The issued device token must contain:
- `sub` / `nameid`: The backend-assigned `DeviceId` (UUID).
- `role`: `ClientAgent` or `GamingStation`.
- `device_code`: The physical gaming station code (e.g. `PC-46D0B13F`).
- `exp`: Expiration timestamp (standard Unix epoch).

---

## 4. Client-to-Server Hub Methods

The client calls the following Hub methods via `hubConnection.SendAsync(methodName, payload)` or `hubConnection.InvokeAsync(methodName, payload)`:

### 4.1 `SendHeartbeat`
Dispatched by the Client Agent on a configurable interval (default: 15 seconds).

**Method Signature:**
```csharp
Task SendHeartbeat(HeartbeatMessage message);
```

**Payload Schema (`HeartbeatMessage`):**
```json
{
  "messageId": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
  "version": "1.0",
  "timestampUtc": "2026-11-24T10:06:20.0403621Z",
  "deviceId": "ce07ed1d-64fc-4b10-9367-7e94412bbe11",
  "sequenceNumber": 104,
  "agentVersion": "1.0.0",
  "upTimeSeconds": 3612.4,
  "cpuPercent": 14.2,
  "memoryUsageMb": 86.5,
  "connectionStatus": "Connected"
}
```

**Field Specifications:**
- `deviceId`: Must match the backend-generated `id` from the `devices` table.
- `sequenceNumber`: Monotonically increasing `int64` per agent process execution.
- `connectionStatus`: Current client connection status (`Connected`, `Reconnecting`, `Disconnected`).
- `timestampUtc`: Client UTC timestamp in ISO 8601 format.

---

## 5. Server-to-Client Hub Events

The backend Hub broadcasts or sends messages to specific client connections using `Clients.Caller.SendAsync(...)` or `Clients.Client(connectionId).SendAsync(...)`.

### 5.1 `HeartbeatAck` (Server Acknowledgement)
**Purpose:** Confirm receipt of a client heartbeat. The Client Agent tracks acknowledgements and will mark heartbeats as missed if no acknowledgement is received within `TimeoutSeconds` (default: 10s).

**Invocation:**
```csharp
await Clients.Caller.SendAsync("HeartbeatAck", new HeartbeatAcknowledgment
{
    SequenceNumber = message.SequenceNumber,
    OriginalMessageId = message.MessageId,
    ServerTimestampUtc = DateTimeOffset.UtcNow
});
```

**Payload Schema (`HeartbeatAcknowledgment`):**
```json
{
  "messageId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "version": "1.0",
  "timestampUtc": "2026-11-24T10:06:20.0850000Z",
  "deviceId": "ce07ed1d-64fc-4b10-9367-7e94412bbe11",
  "sequenceNumber": 104,
  "originalMessageId": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
  "serverTimestampUtc": "2026-11-24T10:06:20.0850000Z"
}
```

---

### 5.2 `DeviceApproval`
**Purpose:** Notifies an agent in `PendingApproval` state that an administrator has approved or rejected its registration.

**Payload Schema:**
```json
{
  "messageId": "d8e9f0a1-b2c3-4d5e-6f7a-8b9c0d1e2f3a",
  "version": "1.0",
  "timestampUtc": "2026-11-24T10:15:00.0000000Z",
  "deviceId": "ce07ed1d-64fc-4b10-9367-7e94412bbe11",
  "isApproved": true,
  "approvalStatus": "Approved",
  "assignedZone": "VIP-Station",
  "rejectionReason": null
}
```

---

### 5.3 `DeviceAuthentication`
**Purpose:** Issues cryptographic session tokens (JWT) to an approved device.

**Payload Schema:**
```json
{
  "messageId": "f1e2d3c4-b5a6-7890-1234-56789abcdef0",
  "version": "1.0",
  "timestampUtc": "2026-11-24T10:15:01.0000000Z",
  "deviceId": "ce07ed1d-64fc-4b10-9367-7e94412bbe11",
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "tokenExpiresAtUtc": "2026-11-24T22:15:01.0000000Z",
  "serverTimeUtc": "2026-11-24T10:15:01.0000000Z"
}
```

---

### 5.4 `DeviceRevocation`
**Purpose:** Immediately revokes authorization and invalidates credentials for decommissioned or locked gaming stations.

**Payload Schema:**
```json
{
  "messageId": "12345678-9abc-def0-1234-56789abcdef0",
  "version": "1.0",
  "timestampUtc": "2026-11-24T11:00:00.0000000Z",
  "deviceId": "ce07ed1d-64fc-4b10-9367-7e94412bbe11",
  "reason": "Station decommissioning initiated by administrator",
  "revokedAtUtc": "2026-11-24T11:00:00.0000000Z"
}
```

---

### 5.5 `DeviceConfiguration`
**Purpose:** Pushes dynamic configuration adjustments (such as adjusted heartbeat frequency or rate limits).

**Payload Schema:**
```json
{
  "messageId": "87654321-cba9-0fed-4321-0fedcba98765",
  "version": "1.0",
  "timestampUtc": "2026-11-24T11:05:00.0000000Z",
  "deviceId": "ce07ed1d-64fc-4b10-9367-7e94412bbe11",
  "configVersion": "2.1",
  "heartbeatIntervalSeconds": 15,
  "parameters": {
    "TelemetrySampleRate": "5s",
    "ScreenLockPolicy": "Strict"
  }
}
```

---

## 6. Server-Side Timeout & Zombie Detection Rules

To maintain high availability and accurate cafe workstation availability dashboards:

1. **Heartbeat Expected Frequency:** Every 15 seconds.
2. **Server Grace Window:** 30 seconds (2 missed intervals) → Mark device status as `Lagging` or `Degraded`.
3. **Server Stale Threshold:** 45 seconds (3 missed intervals) → Mark device status as `Offline` / `Disconnected`.
4. **Connection Reaping:** If WebSocket closes abruptly or TCP RST occurs, invoke `OnDisconnectedAsync` in Hub and mark `device_status` as `Offline` immediately.
5. **Client Self-Healing Recovery:** If the client misses 3 consecutive acknowledgements (`MaxMissedHeartbeats = 3`), the client will forcibly close and re-establish its SignalR connection to heal silent socket dropouts.

---

## 7. Activation Checklist for Backend Team

When ready to enable the SignalR Hub on the backend:

- [ ] Implement `AgentHub : Hub` in `GamingCafe.Api/Hubs/AgentHub.cs`.
- [ ] Map hub endpoint in `Program.cs`: `app.MapHub<AgentHub>("/hubs/agent");`.
- [ ] Add `[Authorize(Roles = "ClientAgent")]` or custom device authorization attribute to `AgentHub`.
- [ ] Implement `SendHeartbeat` method responding with `HeartbeatAck`.
- [ ] Set `"EnableSignalR": true` in Client Agent `appsettings.json` and deploy.
