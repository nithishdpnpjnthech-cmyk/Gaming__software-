using GamingCafe.ClientAgent.Heartbeat;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Realtime;

/// <summary>
/// High-level client facade over the real-time SignalR connection.
/// </summary>
public interface ISignalRHubClient
{
    bool IsConnected { get; }
    Task<bool> SendHeartbeatAsync(HeartbeatPayload payload, CancellationToken cancellationToken = default);
}

public class SignalRHubClient : ISignalRHubClient
{
    private readonly ISignalRConnectionManager _connectionManager;
    private readonly ILogger<SignalRHubClient> _logger;

    public SignalRHubClient(ISignalRConnectionManager connectionManager, ILogger<SignalRHubClient> logger)
    {
        _connectionManager = connectionManager;
        _logger = logger;
    }

    public bool IsConnected => _connectionManager.CurrentState == RealtimeConnectionState.Connected;

    public async Task<bool> SendHeartbeatAsync(HeartbeatPayload payload, CancellationToken cancellationToken = default)
    {
        return await _connectionManager.SendHeartbeatAsync(payload, cancellationToken);
    }
}
