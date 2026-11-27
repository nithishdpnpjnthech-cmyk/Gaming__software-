# SignalR Implementation Specification

## Hub Endpoint & Configuration
- **Route**: `/hubs/agent`
- **Transport**: WebSockets / JSON
- **Authorization**: `[Authorize(Policy = "agent.heartbeat")]`

---

### Lifecycle Events & Operational Tracking

#### 1. Connection Event (`OnConnectedAsync`)
- Verifies JWT bearer token attached to query string `access_token`.
- Extracts `device_id` claim from authenticated caller.
- Inserts audit record in `device_connection_logs`:
  - `event_type = 'CONNECTED'`
  - `details = 'ConnectionId: <Context.ConnectionId>'`
- Updates `device_status`:
  - `is_online = true`
  - `last_seen_at = DateTime.UtcNow`
  - `last_status_change_at = DateTime.UtcNow`

#### 2. Disconnection Event (`OnDisconnectedAsync`)
- Inserts audit record in `device_connection_logs`:
  - `event_type = 'DISCONNECTED'`
  - `details = 'ConnectionId: <Id>; Reason: <SanitizedReason>'`
- Updates `device_status`:
  - `is_online = false`
  - `last_status_change_at = DateTime.UtcNow`

#### 3. Heartbeat Event (`SendHeartbeat`)
- Validates payload: `MessageId`, monotonic `SequenceNumber`, `Version = "1.0"`, `MessageType = "Heartbeat"`.
- Delegates atomic persistence to `IHeartbeatService.RecordAsync`.
- Dispatches `HeartbeatAck` back to caller with server UTC microsecond timestamp.
