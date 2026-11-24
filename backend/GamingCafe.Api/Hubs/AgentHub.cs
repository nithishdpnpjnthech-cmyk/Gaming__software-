using System.Net;
using System.Security.Claims;
using GamingCafe.Api.Data;
using GamingCafe.Api.DTOs;
using GamingCafe.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GamingCafe.Api.Hubs;

[Authorize]
public class AgentHub : Hub
{
    private readonly GamingCafeDbContext _dbContext;
    private readonly ILogger<AgentHub> _logger;

    public AgentHub(
        GamingCafeDbContext dbContext,
        ILogger<AgentHub> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var deviceId = GetDeviceIdFromClaims();
        var clientIp = GetClientIpAddress();

        _logger.LogInformation("Agent connected: ConnectionId={ConnectionId}, DeviceId={DeviceId}, IP={IP}",
            Context.ConnectionId, deviceId, clientIp);

        if (deviceId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"device_{deviceId.Value:D}");

            var status = await _dbContext.DeviceStatuses.FirstOrDefaultAsync(s => s.DeviceId == deviceId.Value);
            var now = DateTime.UtcNow;

            if (status != null)
            {
                status.LastSeenAt = now;
                status.LastIpAddress = clientIp;
                status.ReportedPowerState = "ON";
                status.UpdatedAt = now;
            }

            _dbContext.DeviceConnectionLogs.Add(new DeviceConnectionLog
            {
                DeviceId = deviceId.Value,
                EventType = "CONNECTED",
                EventTime = now,
                Details = $"Agent connected via SignalR WSS. ConnectionId={Context.ConnectionId}"
            });

            await _dbContext.SaveChangesAsync();
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var deviceId = GetDeviceIdFromClaims();

        _logger.LogInformation("Agent disconnected: ConnectionId={ConnectionId}, DeviceId={DeviceId}, Reason={Reason}",
            Context.ConnectionId, deviceId, exception?.Message ?? "Normal closure");

        if (deviceId.HasValue)
        {
            var now = DateTime.UtcNow;
            var status = await _dbContext.DeviceStatuses.FirstOrDefaultAsync(s => s.DeviceId == deviceId.Value);
            if (status != null)
            {
                status.LastSeenAt = now;
                status.UpdatedAt = now;
            }

            _dbContext.DeviceConnectionLogs.Add(new DeviceConnectionLog
            {
                DeviceId = deviceId.Value,
                EventType = "DISCONNECTED",
                EventTime = now,
                Details = exception != null 
                    ? $"Abrupt disconnect: {exception.Message}" 
                    : "Graceful disconnect."
            });

            await _dbContext.SaveChangesAsync();
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendHeartbeat(HeartbeatRequestMessage heartbeat)
    {
        var claimDeviceId = GetDeviceIdFromClaims();
        var targetDeviceId = claimDeviceId ?? heartbeat.DeviceId;
        var clientIp = GetClientIpAddress();

        _logger.LogDebug("Heartbeat received: Device {DeviceId}, Seq #{Seq}, CPU: {Cpu}%, Mem: {Mem}MB",
            targetDeviceId, heartbeat.SequenceNumber, heartbeat.CpuPercent, heartbeat.MemoryUsageMb);

        var now = DateTime.UtcNow;

        _dbContext.DeviceHeartbeats.Add(new DeviceHeartbeat
        {
            DeviceId = targetDeviceId,
            ReceivedAt = now,
            AgentVersion = heartbeat.AgentVersion,
            SourceIp = clientIp
        });

        var status = await _dbContext.DeviceStatuses.FirstOrDefaultAsync(s => s.DeviceId == targetDeviceId);
        if (status != null)
        {
            status.LastSeenAt = now;
            status.LastIpAddress = clientIp;
            status.ReportedPowerState = "ON";
            status.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync();

        await Clients.Caller.SendAsync("HeartbeatAck", new HeartbeatAcknowledgmentMessage
        {
            DeviceId = targetDeviceId,
            SequenceNumber = heartbeat.SequenceNumber,
            OriginalMessageId = heartbeat.MessageId,
            ServerTimestampUtc = DateTimeOffset.UtcNow
        });
    }

    private Guid? GetDeviceIdFromClaims()
    {
        var sub = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) 
               ?? Context.User?.FindFirstValue("device_id");

        if (Guid.TryParse(sub, out var deviceId))
        {
            return deviceId;
        }

        return null;
    }

    private IPAddress? GetClientIpAddress()
    {
        return Context.GetHttpContext()?.Connection.RemoteIpAddress;
    }
}
