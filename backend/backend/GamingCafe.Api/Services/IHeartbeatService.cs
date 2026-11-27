using System.Net;
using GamingCafe.Api.DTOs;
namespace GamingCafe.Api.Services;
public interface IHeartbeatService
{
    Task<HeartbeatAcknowledgment> RecordAsync(HeartbeatMessage message, IPAddress? ip, CancellationToken ct);
}
