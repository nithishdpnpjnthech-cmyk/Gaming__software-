using GamingCafe.Api.DTOs;
using GamingCafe.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
namespace GamingCafe.Api.Hubs;
[Authorize(Policy = "agent.heartbeat")]
public sealed class AgentHub(IHeartbeatService service) : Hub
{
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
}
