using GamingCafe.Api.Data;
using GamingCafe.Api.DTOs;
using GamingCafe.Api.Models;
using GamingCafe.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace GamingCafe.Api.Hubs;

[Authorize(Policy = "agent.heartbeat")]
public sealed class AgentHub(
    IHeartbeatService service,
    GamingCafeDbContext db,
    ILogger<AgentHub> logger) : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        if (TryGetDeviceId(out var deviceId))
        {
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
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to log CONNECTED event for device {DeviceId} (ConnectionId: {ConnectionId}).", deviceId, Context.ConnectionId);
            }
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            if (TryGetDeviceId(out var deviceId))
            {
                try
                {
                    var reason = GetSanitizedDisconnectionReason(exception);

                    var connectionLog = new DeviceConnectionLog
                    {
                        DeviceId = deviceId,
                        EventType = "DISCONNECTED",
                        EventTime = DateTime.UtcNow,
                        Details = $"ConnectionId: {Context.ConnectionId}; Reason: {reason}"
                    };

                    db.DeviceConnectionLogs.Add(connectionLog);
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
        await Clients.Caller.SendAsync("HeartbeatAck", ack, Context.ConnectionAborted);
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
