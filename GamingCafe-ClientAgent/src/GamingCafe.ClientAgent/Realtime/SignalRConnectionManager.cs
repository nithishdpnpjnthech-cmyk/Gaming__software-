using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using GamingCafe.ClientAgent.Authentication;
using GamingCafe.ClientAgent.Communication;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Heartbeat;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GamingCafe.ClientAgent.Realtime;

/// <summary>
/// Contract for the real-time SignalR WebSocket manager.
/// </summary>
public interface ISignalRConnectionManager : IAsyncDisposable
{
    RealtimeConnectionState CurrentState { get; }
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task<bool> SendHeartbeatAsync(HeartbeatPayload payload, CancellationToken cancellationToken = default);
    event Action<RealtimeConnectionState>? StateChanged;
    event Action<EnrollmentStatusChangedEvent>? EnrollmentStatusChanged;
}

/// <summary>
/// Thread-safe SignalR WebSocket connection manager.
/// Enforces WSS over TLS, dynamic access token injection, automatic reconnection,
/// and safe foundational event handling.
/// </summary>
public class SignalRConnectionManager : ISignalRConnectionManager
{
    private readonly IOptions<AgentConfiguration> _options;
    private readonly IDeviceAuthenticationService _authService;
    private readonly ILogger<SignalRConnectionManager> _logger;

    private HubConnection? _hubConnection;
    private RealtimeConnectionState _currentState = RealtimeConnectionState.Disconnected;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    public event Action<RealtimeConnectionState>? StateChanged;
    public event Action<EnrollmentStatusChangedEvent>? EnrollmentStatusChanged;

    public SignalRConnectionManager(
        IOptions<AgentConfiguration> options,
        IDeviceAuthenticationService authService,
        ILogger<SignalRConnectionManager> logger)
    {
        _options = options;
        _authService = authService;
        _logger = logger;
    }

    public RealtimeConnectionState CurrentState
    {
        get
        {
            lock (_lifecycleLock) return _currentState;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (_currentState == RealtimeConnectionState.Connected || _currentState == RealtimeConnectionState.Connecting)
            {
                _logger.LogDebug("SignalR start requested, but connection is already {State}.", _currentState);
                return;
            }

            var isAuthenticated = await _authService.IsAuthenticatedAsync(cancellationToken);
            if (!isAuthenticated)
            {
                _logger.LogWarning("SignalR connection aborted: Device is not yet authenticated with backend.");
                SetState(RealtimeConnectionState.Disconnected);
                return;
            }

            SetState(RealtimeConnectionState.Connecting);
            EnsureHubConnectionBuilt();

            if (_hubConnection == null)
            {
                SetState(RealtimeConnectionState.Failed);
                return;
            }

            _logger.LogInformation("Initiating secure WSS SignalR connection to: {HubUrl}", _options.Value.SignalRHubUrl);
            await _hubConnection.StartAsync(cancellationToken);
            SetState(RealtimeConnectionState.Connected);
            _logger.LogInformation("SignalR real-time WSS connection established successfully.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetState(RealtimeConnectionState.Disconnected);
            _logger.LogInformation("SignalR connection cancelled by service shutdown.");
        }
        catch (Exception ex)
        {
            SetState(RealtimeConnectionState.Failed);
            _logger.LogWarning(ex, "Failed to connect to SignalR Hub at {HubUrl}. Agent will retry automatically.",
                _options.Value.SignalRHubUrl);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (_hubConnection != null && _currentState != RealtimeConnectionState.Disconnected)
            {
                SetState(RealtimeConnectionState.Stopping);
                _logger.LogInformation("Stopping SignalR connection gracefully...");
                await _hubConnection.StopAsync(cancellationToken);
                SetState(RealtimeConnectionState.Disconnected);
                _logger.LogInformation("SignalR connection stopped.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error occurred while gracefully stopping SignalR connection.");
            SetState(RealtimeConnectionState.Disconnected);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task<bool> SendHeartbeatAsync(HeartbeatPayload payload, CancellationToken cancellationToken = default)
    {
        if (CurrentState != RealtimeConnectionState.Connected || _hubConnection == null)
        {
            _logger.LogDebug("SignalR not connected (CurrentState: {State}). Skipping SignalR heartbeat.", CurrentState);
            return false;
        }

        try
        {
            // Proposed hub method: "SendHeartbeat"
            await _hubConnection.InvokeAsync("SendHeartbeat", payload, cancellationToken);
            _logger.LogInformation("SignalR real-time heartbeat dispatched successfully.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR heartbeat invocation failed. Falling back to REST.");
            return false;
        }
    }

    private void EnsureHubConnectionBuilt()
    {
        if (_hubConnection != null) return;

        var hubUrl = _options.Value.SignalRHubUrl;
        var config = _options.Value;

        var builder = new HubConnectionBuilder()
            .WithUrl(hubUrl, (HttpConnectionOptions options) =>
            {
                // Dynamic access token provider injects the DPAPI-protected secret into the handshake
                options.AccessTokenProvider = async () =>
                {
                    return await _authService.GetDeviceSecretAsync();
                };

                // Enforce strict TLS certificate verification
                options.WebSocketConfiguration = wsOptions =>
                {
                    wsOptions.RemoteCertificateValidationCallback = (sender, cert, chain, errors) =>
                    {
                        var x509Cert = cert as X509Certificate2 ?? (cert != null ? new X509Certificate2(cert) : null);
                        return BackendApiClient.ValidateServerCertificate(
                            new HttpRequestMessage(), x509Cert, chain, errors, config);
                    };
                };

                options.HttpMessageHandlerFactory = handler =>
                {
                    if (handler is HttpClientHandler clientHandler)
                    {
                        clientHandler.ServerCertificateCustomValidationCallback = (req, cert, chain, errors) =>
                            BackendApiClient.ValidateServerCertificate(req, cert, chain, errors, config);
                    }
                    return handler;
                };
            })
            .WithAutomaticReconnect(new SignalRReconnectPolicy(config));

        _hubConnection = builder.Build();

        // Register lifecycle event hooks
        _hubConnection.Reconnecting += ex =>
        {
            SetState(RealtimeConnectionState.Reconnecting);
            _logger.LogWarning(ex, "SignalR connection dropped. Reconnecting with exponential backoff & jitter...");
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += connectionId =>
        {
            SetState(RealtimeConnectionState.Connected);
            _logger.LogInformation("SignalR reconnected successfully. ConnectionId: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };

        _hubConnection.Closed += ex =>
        {
            SetState(RealtimeConnectionState.Disconnected);
            if (ex != null)
            {
                _logger.LogWarning(ex, "SignalR connection closed unexpectedly.");
            }
            else
            {
                _logger.LogInformation("SignalR connection closed.");
            }
            return Task.CompletedTask;
        };

        // Foundational safe events (No arbitrary code execution)
        _hubConnection.On<DeviceStatusAcknowledgedEvent>("DeviceStatusAcknowledged", evt =>
        {
            _logger.LogInformation("Received real-time DeviceStatusAcknowledged from server. Acknowledged: {Ack}", evt.Acknowledged);
        });

        _hubConnection.On<EnrollmentStatusChangedEvent>("EnrollmentStatusChanged", evt =>
        {
            _logger.LogInformation("Received real-time EnrollmentStatusChanged: NewStatus={NewStatus}", evt.NewStatus);
            EnrollmentStatusChanged?.Invoke(evt);
        });

        _hubConnection.On<ServerHealthNotificationEvent>("ServerHealthNotification", evt =>
        {
            _logger.LogInformation("Server health notice: Status={Status}, Message={Message}", evt.ServerStatus, evt.Message);
        });

        _hubConnection.On<MaintenanceNotificationEvent>("MaintenanceNotification", evt =>
        {
            _logger.LogWarning("Planned maintenance notification: {NoticeMessage} (Duration: {Duration}m)",
                evt.NoticeMessage, evt.DurationMinutes);
        });
    }

    private void SetState(RealtimeConnectionState newState)
    {
        lock (_lifecycleLock)
        {
            if (_currentState != newState)
            {
                _logger.LogInformation("SignalR Connection State: [{OldState}] -> [{NewState}]", _currentState, newState);
                _currentState = newState;
                StateChanged?.Invoke(newState);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
        _lifecycleLock.Dispose();
    }
}

/// <summary>
/// Custom SignalR reconnect policy providing exponential backoff with cryptographic jitter.
/// </summary>
public class SignalRReconnectPolicy : Microsoft.AspNetCore.SignalR.Client.IRetryPolicy
{
    private readonly AgentConfiguration _options;

    public SignalRReconnectPolicy(AgentConfiguration options)
    {
        _options = options;
    }

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        var minSec = _options.MinRetryBackoffSeconds;
        var maxSec = _options.MaxRetryBackoffSeconds;
        var jitterFactor = _options.BackoffJitterFactor;

        var attempt = (int)Math.Min(retryContext.PreviousRetryCount, 10);
        var baseSec = minSec * Math.Pow(2, attempt);
        var cappedSec = Math.Min(baseSec, maxSec);

        var randomDouble = RandomNumberGenerator.GetInt32(0, 10000) / 10000.0;
        var multiplier = 1.0 - jitterFactor + (2.0 * jitterFactor * randomDouble);
        var finalSec = Math.Clamp(cappedSec * multiplier, minSec, maxSec);

        return TimeSpan.FromSeconds(finalSec);
    }
}
