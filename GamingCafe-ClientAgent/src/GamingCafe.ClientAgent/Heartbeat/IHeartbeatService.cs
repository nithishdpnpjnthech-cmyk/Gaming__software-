using GamingCafe.ClientAgent.Messaging;

namespace GamingCafe.ClientAgent.Heartbeat;

public interface IHeartbeatService
{
    long SequenceNumber { get; }
    int MissedHeartbeats { get; }
    double LastLatencyMs { get; }
    Task<bool> SendHeartbeatAsync(CancellationToken cancellationToken = default);
    void HandleAcknowledgment(HeartbeatAcknowledgment ack);
    void ResetMissedCount();
}
