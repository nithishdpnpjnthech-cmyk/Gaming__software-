# Heartbeat Implementation

## Design & Timings
- **Default Heartbeat Interval**: 15 seconds.
- **Heartbeat Timeout**: 45 seconds (3 missed heartbeats).
- **Offline Monitor Loop**: Runs every 10 seconds.

## Telemetry Pipeline
1. `AgentHub.SendHeartbeat` validates protocol version (`1.0`), message type (`Heartbeat`), monotonic sequence number, and agent version.
2. `HeartbeatService.RecordAsync` acquires row lock on `devices` (`SELECT ... FOR UPDATE`), validates `RegistrationStatus == "APPROVED"`, inserts telemetry row into `device_heartbeats`, and upserts `device_status`.
3. Returns `HeartbeatAcknowledgment` containing `OriginalMessageId`, `SequenceNumber`, and microsecond `ServerTimestampUtc`.

## Offline Monitor Service (`DeviceOfflineMonitorService`)
A `BackgroundService` hosted in the backend runs every 10 seconds:
```csharp
var thresholdUtc = DateTime.UtcNow.AddSeconds(-timeoutSeconds);
var timedOut = await db.DeviceStatuses
    .Where(s => s.IsOnline && (s.LastSeenAt == null || s.LastSeenAt < thresholdUtc))
    .ToListAsync(stoppingToken);

foreach (var status in timedOut)
{
    status.IsOnline = false;
    db.DeviceConnectionLogs.Add(new DeviceConnectionLog {
        EventType = "DISCONNECTED",
        Details = $"Heartbeat timeout exceeded ({timeoutSeconds}s)."
    });
}
```
