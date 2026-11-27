
using System.Net;
using GamingCafe.Api.Data;
using GamingCafe.Api.DTOs;
using GamingCafe.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

namespace GamingCafe.Api.Services;

public sealed class HeartbeatService(
    GamingCafeDbContext db,
    IDeviceMonitoringConsole? monitoringConsole = null) : IHeartbeatService
{
    // Normalize timestamps to PostgreSQL microsecond precision.
    private static DateTime GetUtcNow()
    {
        var now = DateTime.UtcNow;

        return new DateTime(
            now.Ticks - (now.Ticks % 10),
            DateTimeKind.Utc);
    }

    public async Task<HeartbeatAcknowledgment> RecordAsync(
        HeartbeatMessage message,
        IPAddress? ip,
        CancellationToken ct)
    {
        await using var tx =
            await db.Database.BeginTransactionAsync(ct);

        // Serialize heartbeat writes and approval changes
        // for this device.
        var devices = await db.Devices
            .FromSqlInterpolated(
                $"SELECT * FROM public.devices WHERE id = {message.DeviceId} FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(ct);

        if (devices.Count != 1 ||
            devices[0].RegistrationStatus != "APPROVED")
        {
            throw new HubException(
                "Device is not approved.");
        }

        // Use PostgreSQL-compatible server UTC timestamp.
        var now = GetUtcNow();

        // Update agent version if provided and changed
        if (!string.IsNullOrWhiteSpace(message.AgentVersion) &&
            devices[0].AgentVersion != message.AgentVersion)
        {
            devices[0].AgentVersion = message.AgentVersion.Length > 30
                ? message.AgentVersion[..30]
                : message.AgentVersion;
            devices[0].UpdatedAt = now;
        }

        db.DeviceHeartbeats.Add(new DeviceHeartbeat
        {
            DeviceId = message.DeviceId,
            ReceivedAt = now,
            AgentVersion = message.AgentVersion,
            SourceIp = ip
        });

        // Detect if device is recovering from an offline state
        var existingStatus = await db.DeviceStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.DeviceId == message.DeviceId, ct);

        bool wasOffline = existingStatus == null ||
                          existingStatus.LastSeenAt == null ||
                          (existingStatus.LastStatusChangeAt != null && existingStatus.LastStatusChangeAt > existingStatus.LastSeenAt);

        if (wasOffline)
        {
            db.DeviceConnectionLogs.Add(new DeviceConnectionLog
            {
                DeviceId = message.DeviceId,
                EventType = "CONNECTED",
                EventTime = now,
                Details = "Heartbeat received. Device restored to ONLINE."
            });

            monitoringConsole?.LogDeviceReconnected(
                message.DeviceId,
                devices[0].DeviceCode,
                ip?.ToString());
        }

        monitoringConsole?.LogHeartbeat(
            message.DeviceId,
            devices[0].DeviceCode,
            message.SequenceNumber,
            message.AgentVersion,
            ip?.ToString(),
            now,
            isStatusTransition: wasOffline);

        await db.SaveChangesAsync(ct);

        // Insert or update the device status.
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO public.device_status
            (
                device_id,
                last_seen_at,
                last_ip_address,
                last_status_change_at,
                updated_at
            )
            VALUES
            (
                {message.DeviceId},
                {now},
                {ip},
                {now},
                {now}
            )
            ON CONFLICT (device_id) DO UPDATE SET
                last_seen_at = EXCLUDED.last_seen_at,
                last_ip_address = EXCLUDED.last_ip_address,
                last_status_change_at = COALESCE(
                    device_status.last_status_change_at,
                    EXCLUDED.last_status_change_at
                ),
                updated_at = GREATEST(
                    device_status.updated_at,
                    EXCLUDED.updated_at
                )
            WHERE device_status.last_seen_at IS NULL
                OR device_status.last_seen_at
                    <= EXCLUDED.last_seen_at",
            ct);

        // Acknowledge only after successful database commit.
        await tx.CommitAsync(ct);

        return new HeartbeatAcknowledgment
        {
            DeviceId = message.DeviceId,
            SequenceNumber = message.SequenceNumber,
            OriginalMessageId = message.MessageId,
            TimestampUtc = now,
            ServerTimestampUtc = now
        };
    }
}
