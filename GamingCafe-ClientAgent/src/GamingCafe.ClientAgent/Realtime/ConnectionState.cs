namespace GamingCafe.ClientAgent.Realtime;

/// <summary>
/// Connection states of the real-time SignalR WebSocket communication manager.
/// </summary>
public enum RealtimeConnectionState
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
    Reconnecting = 3,
    Stopping = 4,
    Failed = 5
}
