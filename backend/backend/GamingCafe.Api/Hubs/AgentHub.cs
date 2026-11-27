using System.Collections.Concurrent;
using GamingCafe.Api.Data;
using GamingCafe.Api.DTOs;
using GamingCafe.Api.Models;
using GamingCafe.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GamingCafe.Api.Hubs;

[Authorize(Policy = "agent.heartbeat")]
public sealed class AgentHub(
    IHeartbeatService service,
    GamingCafeDbContext db,
    ILogger<AgentHub> logger,
    IDeviceMonitoringConsole? monitoringConsole = null) : Hub
{
    private static readonly ConcurrentDictionary<Guid, string> ActiveConnections = new();

    public static bool IsDeviceConnected(Guid deviceId) => ActiveConnections.ContainsKey(deviceId);

    public static string? GetConnectionId(Guid deviceId) =>
        ActiveConnections.TryGetValue(deviceId, out var connId) ? connId : null;

    public static IReadOnlyDictionary<Guid, string> GetActiveConnections() => ActiveConnections;

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        if (TryGetDeviceId(out var deviceId))
        {
            // Verify device exists and is approved before admitting to operational hub
            var device = await db.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == deviceId);

            if (device == null || device.RegistrationStatus != "APPROVED")
            {
                logger.LogWarning("SignalR connection rejected: DeviceId {DeviceId} is not APPROVED (Status: '{Status}'). Aborting connection.",
                    deviceId, device?.RegistrationStatus ?? "NOT_FOUND");
                Context.Abort();
                return;
            }

            var clientIp = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();
            var isReconnect = ActiveConnections.TryGetValue(deviceId, out var previousConnectionId) && previousConnectionId != Context.ConnectionId;

            // Safe handling of duplicate/reconnecting sessions
            if (isReconnect)
            {
                logger.LogInformation("[RECONNECT] Duplicate/reconnected SignalR session detected. BackendDeviceId: {DeviceId}, ConnectionId: '{ConnectionId}', Superseded: '{OldConn}', Outcome: SUCCESS",
                    deviceId, Context.ConnectionId, previousConnectionId);
            }
            else
            {
                logger.LogInformation("[SIGNALR] Device connected. BackendDeviceId: {DeviceId}, ConnectionId: '{ConnectionId}', Outcome: SUCCESS",
                    deviceId, Context.ConnectionId);
            }
            ActiveConnections[deviceId] = Context.ConnectionId;

            monitoringConsole?.LogSignalRConnected(
                deviceId,
                device.DeviceCode,
                Context.ConnectionId,
                clientIp,
                isReconnect);

            // Join device-specific SignalR group for targeted command dispatching
            await Groups.AddToGroupAsync(Context.ConnectionId, $"device:{deviceId}");

            try
            {
                var connectionLog = new DeviceConnectionLog
                {
                    DeviceId = deviceId,
                    EventType = "CONNECTED",
                    EventTime = DateTime.UtcNow,
                    Details = $"ConnectionId: {Context.ConnectionId}"
                };

                db.DeviceConnectionLogs.Add(connectionLog);

                // Update device_status to online
                var status = await db.DeviceStatuses.FirstOrDefaultAsync(s => s.DeviceId == deviceId);
                var now = DateTime.UtcNow;
                if (status != null)
                {
                    status.LastSeenAt = now;
                    status.LastStatusChangeAt = now;
                    status.UpdatedAt = now;
                }
                else
                {
                    db.DeviceStatuses.Add(new DeviceStatus
                    {
                        DeviceId = deviceId,
                        LastSeenAt = now,
                        LastStatusChangeAt = now,
                        UpdatedAt = now
                    });
                }

                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to log CONNECTED event for device {DeviceId} (ConnectionId: {ConnectionId}).", deviceId, Context.ConnectionId);
            }
        }
        else
        {
            logger.LogWarning("SignalR connection attempt missing or invalid device_id claim! ConnectionId: '{ConnectionId}'", Context.ConnectionId);
            Context.Abort();
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            if (TryGetDeviceId(out var deviceId))
            {
                if (ActiveConnections.TryGetValue(deviceId, out var currentConn) && currentConn == Context.ConnectionId)
                {
                    ActiveConnections.TryRemove(deviceId, out _);
                }

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"device:{deviceId}");

                var reason = GetSanitizedDisconnectionReason(exception);

                logger.LogInformation("[SIGNALR] Device disconnected. BackendDeviceId: {DeviceId}, ConnectionId: '{ConnectionId}', Reason: '{Reason}'",
                    deviceId, Context.ConnectionId, reason);

                var device = await db.Devices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == deviceId);

                monitoringConsole?.LogSignalRDisconnected(
                    deviceId,
                    device?.DeviceCode,
                    Context.ConnectionId,
                    reason);

                try
                {
                    var connectionLog = new DeviceConnectionLog
                    {
                        DeviceId = deviceId,
                        EventType = "DISCONNECTED",
                        EventTime = DateTime.UtcNow,
                        Details = $"ConnectionId: {Context.ConnectionId}; Reason: {reason}"
                    };

                    db.DeviceConnectionLogs.Add(connectionLog);

                    // Update device_status to offline
                    var status = await db.DeviceStatuses.FirstOrDefaultAsync(s => s.DeviceId == deviceId);
                    var now = DateTime.UtcNow;
                    if (status != null)
                    {
                        status.LastStatusChangeAt = now;
                        status.UpdatedAt = now;
                    }

                    await db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to log DISCONNECTED event for device {DeviceId} (ConnectionId: {ConnectionId}).", deviceId, Context.ConnectionId);
                }
            }
        }
        finally
        {
            await base.OnDisconnectedAsync(exception);
        }
    }

    public async Task SendHeartbeat(HeartbeatMessage message)
    {
        if (message is null || message.DeviceId == Guid.Empty ||
            !Guid.TryParse(Context.User?.FindFirst("device_id")?.Value, out var id) || id != message.DeviceId)
            throw new HubException("Invalid device identity.");
        if (string.IsNullOrWhiteSpace(message.MessageId) || message.MessageId.Length > 128 ||
            message.Version != "1.0" || message.MessageType != "Heartbeat" || message.SequenceNumber <= 0 ||
            message.TimestampUtc == default || string.IsNullOrWhiteSpace(message.AgentVersion) || message.AgentVersion.Length > 30)
            throw new HubException("Invalid heartbeat payload.");
        var ip = Context.GetHttpContext()?.Connection.RemoteIpAddress;
        if (ip?.IsIPv4MappedToIPv6 == true) ip = ip.MapToIPv4();
        var ack = await service.RecordAsync(message, ip, Context.ConnectionAborted);
        logger.LogInformation("[HEARTBEAT] Heartbeat received and persisted. BackendDeviceId: {DeviceId}, Seq: {Seq}, Outcome: SUCCESS",
            message.DeviceId, message.SequenceNumber);
        await Clients.Caller.SendAsync("HeartbeatAck", ack, Context.ConnectionAborted);
    }

    public async Task AcknowledgeCommand(Guid commandId, string status, string? errorMessage = null)
    {
        if (!TryGetDeviceId(out var deviceId))
            throw new HubException("Invalid device identity.");

        if (commandId == Guid.Empty || string.IsNullOrWhiteSpace(status))
            throw new HubException("Invalid command acknowledgement payload.");

        var cmd = await db.DeviceCommands
            .FirstOrDefaultAsync(c => c.Id == commandId && c.DeviceId == deviceId);

        if (cmd != null)
        {
            var now = DateTime.UtcNow;
            cmd.CommandStatus = status.Length > 30 ? status[..30] : status;
            cmd.CompletedAt = now;
            cmd.ErrorMessage = errorMessage;

            db.DeviceCommandLogs.Add(new DeviceCommandLog
            {
                CommandId = cmd.Id,
                EventType = status.Length > 50 ? status[..50] : status,
                Details = errorMessage ?? $"Command {cmd.CommandType} status updated to {status}",
                EventTime = now
            });

            await db.SaveChangesAsync();
            logger.LogInformation("Device {DeviceId} acknowledged command {CommandId} with status '{Status}'",
                deviceId, commandId, status);
        }
        else
        {
            logger.LogWarning("Device {DeviceId} attempted to acknowledge non-existent or unauthorized command {CommandId}",
                deviceId, commandId);
        }
    }

    private bool TryGetDeviceId(out Guid deviceId)
    {
        var claimValue = Context.User?.FindFirst("device_id")?.Value;
        return Guid.TryParse(claimValue, out deviceId) && deviceId != Guid.Empty;
    }

    private static string GetSanitizedDisconnectionReason(Exception? exception)
    {
        return exception switch
        {
            null => "Graceful disconnect",
            TimeoutException => "Connection timed out",
            OperationCanceledException => "Connection aborted",
            HubException => "Hub protocol violation",
            _ => "Transport error"
        };
    }
}
