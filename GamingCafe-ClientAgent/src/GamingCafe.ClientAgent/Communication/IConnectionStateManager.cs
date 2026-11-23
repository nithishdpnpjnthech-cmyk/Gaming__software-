using GamingCafe.ClientAgent.Messaging;

namespace GamingCafe.ClientAgent.Communication;

public interface IConnectionStateManager
{
    AgentConnectionStatus CurrentStatus { get; }
    DateTimeOffset? LastConnectedUtc { get; }
    DateTimeOffset? LastDisconnectedUtc { get; }
    int ReconnectAttempts { get; }
    string? LastDisconnectReason { get; }
    event Action<AgentConnectionStatus>? StatusChanged;
    void SetStatus(AgentConnectionStatus status, string? reason = null);
    void IncrementReconnectAttempts();
    void ResetReconnectAttempts();
}
