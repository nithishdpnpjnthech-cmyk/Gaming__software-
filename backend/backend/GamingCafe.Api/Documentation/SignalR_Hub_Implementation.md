# SignalR Hub Implementation

## AgentHub Architecture
Mounted at `/hubs/agent`, `AgentHub` serves as the persistent duplex communication bridge.

## 1. Authentication & Policy
- Gated by authorization policy `[Authorize(Policy = "agent.heartbeat")]`.
- Policy requires the caller principal to possess the `"device_id"` claim.
- Token is extracted from the query string during WebSocket handshake (`access_token`).

## 2. Connection Lifecycle
```csharp
public override async Task OnConnectedAsync()
{
    // 1. Extract device_id claim
    // 2. Add CONNECTED row to device_connection_logs
    // 3. Mark device_status.is_online = true and last_seen_at = now
    // 4. SaveChangesAsync
}

public override async Task OnDisconnectedAsync(Exception? ex)
{
    // 1. Add DISCONNECTED row to device_connection_logs
    // 2. Mark device_status.is_online = false
    // 3. SaveChangesAsync
}
```

## 3. Duplex Invocations
- **Agent -> Hub**: `SendHeartbeat(HeartbeatMessage message)`
- **Hub -> Agent**: `Clients.Caller.SendAsync("HeartbeatAck", ack)`
