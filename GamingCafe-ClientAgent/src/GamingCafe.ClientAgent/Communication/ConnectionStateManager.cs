using GamingCafe.ClientAgent.Messaging;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Communication;

public sealed class ConnectionStateManager : IConnectionStateManager
{
    private readonly ILogger<ConnectionStateManager> _logger;
    private AgentConnectionStatus _currentStatus = AgentConnectionStatus.Disconnected;
    private DateTimeOffset? _lastConnectedUtc;
    private DateTimeOffset? _lastDisconnectedUtc;
    private int _reconnectAttempts;
    private string? _lastDisconnectReason;
    private readonly object _lock = new();

    public event Action<AgentConnectionStatus>? StatusChanged;

    public ConnectionStateManager(ILogger<ConnectionStateManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public AgentConnectionStatus CurrentStatus
    {
        get
        {
            lock (_lock) return _currentStatus;
        }
    }

    public DateTimeOffset? LastConnectedUtc
    {
        get
        {
            lock (_lock) return _lastConnectedUtc;
        }
    }

    public DateTimeOffset? LastDisconnectedUtc
    {
        get
        {
            lock (_lock) return _lastDisconnectedUtc;
        }
    }

    public int ReconnectAttempts
    {
        get
        {
            lock (_lock) return _reconnectAttempts;
        }
    }

    public string? LastDisconnectReason
    {
        get
        {
            lock (_lock) return _lastDisconnectReason;
        }
    }

    public void SetStatus(AgentConnectionStatus status, string? reason = null)
    {
        AgentConnectionStatus oldStatus;
        lock (_lock)
        {
            if (_currentStatus == status) return;

            oldStatus = _currentStatus;
            _currentStatus = status;

            if (status == AgentConnectionStatus.Connected)
            {
                _lastConnectedUtc = DateTimeOffset.UtcNow;
                _reconnectAttempts = 0;
            }
            else if (status == AgentConnectionStatus.Disconnected)
            {
                _lastDisconnectedUtc = DateTimeOffset.UtcNow;
                _lastDisconnectReason = reason;
            }
        }

        _logger.LogInformation("Connection status transition: {OldStatus} -> {NewStatus} (Reason: {Reason})",
            oldStatus, status, reason ?? "Normal");
        StatusChanged?.Invoke(status);
    }

    public void IncrementReconnectAttempts()
    {
        lock (_lock)
        {
            _reconnectAttempts++;
        }
    }

    public void ResetReconnectAttempts()
    {
        lock (_lock)
        {
            _reconnectAttempts = 0;
        }
    }
}
