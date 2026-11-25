# Gaming Cafe Backend Integration Contract Specification

**Module:** Client Agent ↔ Backend Web API & SignalR Hub  
**Document:** `BACKEND-INTEGRATION-CONTRACT.md`  
**Target Systems:**  
- **Client Agent:** `GamingCafe.ClientAgent` (.NET 10 Background Service)  
- **Backend API:** `GamingCafe.Api` (ASP.NET Core .NET 10 Web API)  
**Status:** **Awaiting Backend Team Implementation**  
**Current Client Configuration:** `EnableSignalR: false` (Dormant until backend Hub deployment)

---

## 1. Executive Summary & Context

The Gaming Cafe Client Agent requires bidirectional, real-time communication with the central management backend over **Secure WebSockets (WSS)** via **ASP.NET Core SignalR**.

An architectural audit of `GamingCafe.Api` determined that:
1. REST registration exists at `POST /api/devices/register` and works end-to-end.
2. Administrative approval endpoints exist (`/api/devices/pending`, `/api/devices/{id}/approve`, `/api/devices/{id}/reject`), but only accept Admin JWTs and do not push events to client PCs.
3. **No SignalR Hub, Hub routing (`MapHub`), or WebSocket endpoint currently exists on the backend.**

To prevent client connection failure loops and unhandled socket exceptions, the Client Agent operates with **`EnableSignalR: false`**. All client-side SignalR infrastructure, WSS protocol handlers, heartbeat tracking, and security validation are fully implemented and verified against local in-memory WebSocket test servers.

This specification provides the backend team with the exact contracts required to implement `AgentHub` in `GamingCafe.Api`.

---

## 2. SignalR Hub URL & Transport Requirements

### 2.1 Transport Protocol
- **Transport Type:** Strictly `HttpTransportType.WebSockets`.
- **Fallbacks:** Long Polling and Server-Sent Events (SSE) are explicitly disabled in the client agent.
- **Protocol:** `WSS` (`wss://`) is strictly enforced in production. Plain `ws://` and `http://` are rejected by `SecurityValidationService`.

### 2.2 Hub Endpoint Routes
| Environment | Hub Route | Client Scheme |
| :--- | :--- | :--- |
| **Production** | `/hubs/agent` | `wss://admin.gamingcafe.local:7267/hubs/agent` |
| **Development** | `/hubs/agent` | `wss://localhost:5001/hubs/agent` (or `http://localhost:5022/hubs/agent`) |

### 2.3 Backend Setup Requirement
In `backend/GamingCafe.Api/Program.cs`:
```csharp
builder.Services.AddSignalR();
...
app.MapHub<AgentHub>("/hubs/agent");
```

---

## 3. Connection Authorization & Authentication Requirements

### 3.1 Token Transmission
The Client Agent supplies a JWT Bearer token dynamically via SignalR's `AccessTokenProvider`:
```csharp
options.AccessTokenProvider = () => _authService.GetAccessTokenAsync();
```
- During initial WebSocket HTTP upgrade negotiation, SignalR transmits this token in the `Authorization: Bearer <token>` header (or as `access_token` query parameter).

### 3.2 Required JWT Claims
The issued device authentication token must include:
- `sub` (or `ClaimTypes.NameIdentifier`): The backend-assigned Device UUID (`devices.id`).
- `device_code`: Physical station code (e.g., `PC-46D0B13F`).
- `role`: `GamingStation` or `ClientAgent`.
- `exp`: Expiration timestamp (standard Unix epoch).

### 3.3 Authorization Failure
If the token is invalid, expired, or belongs to a revoked device:
- The backend Hub must reject the connection during `OnConnectedAsync` with an HTTP 401 Unauthorized or terminate the WebSocket with `WebSocketCloseStatus.PolicyViolation`.

---

## 4. Device Connection Registration

Upon connection, the backend `AgentHub` must associate the SignalR `Context.ConnectionId` with the authenticated `DeviceId`:
```csharp
public override async Task OnConnectedAsync()
{
    var deviceId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(deviceId))
    {
        Context.Abort();
        return;
    }

    // Map device connection to group or memory cache
    await Groups.AddToGroupAsync(Context.ConnectionId, $"device_{deviceId}");
    await base.OnConnectedAsync();
}
```

---

## 5. Client-to-Server Hub Methods

### 5.1 `SendHeartbeat` Request Contract

Dispatched periodically by the Client Agent (default: every 15 seconds) when in `Approved` or `Authenticated` state.

#### Method Name
`SendHeartbeat`

#### C# Request DTO Schema (`HeartbeatMessage`)
```csharp
public class HeartbeatMessage
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString("N");
    public string Version { get; set; } = "1.0";
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid DeviceId { get; set; }             // Backend Device UUID
    public string MessageType => "Heartbeat";
    public long SequenceNumber { get; set; }        // Monotonic sequence counter
    public string AgentVersion { get; set; }        // e.g. "1.0.0"
    public double UpTimeSeconds { get; set; }       // System uptime
    public double CpuPercent { get; set; }          // CPU utilization 0.0 - 100.0
    public double MemoryUsageMb { get; set; }       // Working set memory in MB
    public AgentConnectionStatus ConnectionStatus { get; set; } // "Connected"
}
```

#### JSON Payload Sample
```json
{
  "messageId": "9b1deb4d3b7d4bad9bdd2b0d7b3dcb6d",
  "version": "1.0",
  "timestampUtc": "2026-11-25T10:15:30.1234567Z",
  "deviceId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "messageType": "Heartbeat",
  "sequenceNumber": 42,
  "agentVersion": "1.0.0",
  "upTimeSeconds": 7200.5,
  "cpuPercent": 18.4,
  "memoryUsageMb": 94.2,
  "connectionStatus": "Connected"
}
```

---

## 6. Server-to-Client Hub Events

### 6.1 `HeartbeatAck` Response Contract

**Mandatory Requirement:** The Client Agent tracks heartbeats in-flight and will **not** mark a heartbeat successful until `HeartbeatAck` is received. If no ACK arrives within `TimeoutSeconds` (default: 10s), the heartbeat is marked as missed. If missed count reaches `MaxMissedHeartbeats` (default: 3), the agent forces socket recovery.

#### Method Invocation from Hub
```csharp
await Clients.Caller.SendAsync("HeartbeatAck", new HeartbeatAcknowledgment
{
    DeviceId = message.DeviceId,
    SequenceNumber = message.SequenceNumber,
    OriginalMessageId = message.MessageId,
    ServerTimestampUtc = DateTimeOffset.UtcNow
});
```

#### C# Event DTO Schema (`HeartbeatAcknowledgment`)
```csharp
public class HeartbeatAcknowledgment
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString("N");
    public string MessageType => "HeartbeatAck";
    public Guid DeviceId { get; set; }
    public long SequenceNumber { get; set; }        // Must echo client's SequenceNumber
    public string OriginalMessageId { get; set; }   // Echoes client's Heartbeat MessageId
    public DateTimeOffset ServerTimestampUtc { get; set; }
}
```

---

### 6.2 `DeviceApproval` Event

When an administrator approves a station in the management portal (`POST /api/devices/{id}/approve`), the backend must push this event to the station:

#### Method Invocation from Hub / Controller
```csharp
await hubContext.Clients.Group($"device_{deviceId}").SendAsync("DeviceApproval", new DeviceApprovalMessage
{
    DeviceId = deviceId,
    IsApproved = true,
    ApprovalStatus = EnrollmentState.Approved,
    AssignedZone = "VIP-Zone",
    RejectionReason = null
});
```

#### C# Event DTO Schema (`DeviceApprovalMessage`)
```csharp
public class DeviceApprovalMessage
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString("N");
    public string MessageType => "DeviceApproval";
    public Guid DeviceId { get; set; }
    public bool IsApproved { get; set; }
    public string? AssignedZone { get; set; }
    public string? RejectionReason { get; set; }
}
```

---

### 6.3 `DeviceRevocation` Event

When an administrator revokes a station's authorization or decommissions it:

#### Method Invocation from Hub / Controller
```csharp
await hubContext.Clients.Group($"device_{deviceId}").SendAsync("DeviceRevocation", new DeviceRevocationMessage
{
    DeviceId = deviceId,
    Reason = "Station decommissioned by administrator.",
    RevokedAtUtc = DateTimeOffset.UtcNow
});
```

#### C# Event DTO Schema (`DeviceRevocationMessage`)
```csharp
public class DeviceRevocationMessage
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString("N");
    public string MessageType => "DeviceRevocation";
    public Guid DeviceId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset RevokedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
```

---

## 7. Error Responses & Reconnection Expectations

### 7.1 Backend Error Notification (`ErrorMessage`)
If an operation on the Hub encounters an error, the backend may emit an `ErrorMessage`:
```csharp
await Clients.Caller.SendAsync("ErrorMessage", new ErrorMessage
{
    ErrorCode = "RATE_LIMIT_EXCEEDED",
    ErrorDescription = "Heartbeat rate limit exceeded. Send at most once per 5 seconds."
});
```

### 7.2 Reconnection Behavior
When the backend server restarts or network connection drops:
1. The Client Agent utilizes **exponential backoff with cryptographic jitter**:
   $$\text{Delay} = \min(\text{MaxDelay}, \text{InitialDelay} \times \text{Multiplier}^{\text{attempt}-1}) + \text{CryptoJitter}$$
2. The agent automatically transitions to `Reconnecting`.
3. Once the backend Hub becomes reachable, SignalR restores the WebSocket and triggers the `Reconnected` handler.
4. The client resets retry attempts, resets the missed heartbeat counter, and resumes heartbeat transmission.

---

## 8. Testing Boundary: Local vs. Backend Integration

| Feature / Capability | Testable Locally Without Backend? | How Verified Locally | Requires Real Backend? |
| :--- | :---: | :--- | :---: |
| **REST Registration (`POST /api/devices/register`)** | **Yes** | Mock HTTP handlers & live backend `DevicesController` | No |
| **DPAPI Identity Persistence** | **Yes** | Isolated filesystem & encrypted byte verification | No |
| **SignalR HubConnection Lifecycle** | **Yes** | In-process Kestrel SignalR test server over WSS (`RealWebSocketConnectionTests`) | No |
| **Strict WSS & TLS Validation** | **Yes** | In-memory self-signed X509 test certificates with thumbprint pinning | No |
| **Heartbeat Monotonic Sequence** | **Yes** | Unit test assertions on `Interlocked.Increment` | No |
| **HeartbeatAck Latency Calculation** | **Yes** | Loopback WebSocket frame roundtrip | No |
| **Heartbeat Timeout & Socket Reset** | **Yes** | Mock unacknowledged timeouts | No |
| **Exponential Backoff & Full Jitter** | **Yes** | Mathematical progression & random boundary tests | No |
| **Server Restart Recovery** | **Yes** | Starting, stopping, and restarting Kestrel test server on the same port | No |
| **Real Backend Approval Flow** | **No** | Requires backend to push `DeviceApproval` event or offer polling API | **YES** |
| **Real Backend Heartbeat Telemetry** | **No** | Requires backend `AgentHub` deployed in production | **YES** |
| **Real Backend Device Revocation** | **No** | Requires backend admin action | **YES** |
