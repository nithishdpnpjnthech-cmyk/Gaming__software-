using GamingCafe.ClientAgent.Messaging;

namespace GamingCafe.ClientAgent.Communication;

public interface ISignalRConnectionManager : IAsyncDisposable
{
    bool IsConnected { get; }
    string? ConnectionId { get; }
    AgentConnectionStatus Status { get; }
    event Action<AgentConnectionStatus, string?>? ConnectionStateChanged;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task SendAsync(string methodName, object? arg1, CancellationToken cancellationToken = default);
    Task<T> InvokeAsync<T>(string methodName, object? arg1, CancellationToken cancellationToken = default);
}
