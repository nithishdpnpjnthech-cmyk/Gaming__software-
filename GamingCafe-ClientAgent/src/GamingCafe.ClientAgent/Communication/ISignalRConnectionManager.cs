using GamingCafe.ClientAgent.Messaging;

namespace GamingCafe.ClientAgent.Communication;

public interface ISignalRConnectionManager : IAsyncDisposable
{
    bool IsConnected { get; }
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task SendAsync(string methodName, object? arg1, CancellationToken cancellationToken = default);
    Task<T> InvokeAsync<T>(string methodName, object? arg1, CancellationToken cancellationToken = default);
}
