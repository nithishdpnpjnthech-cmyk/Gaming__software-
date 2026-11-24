using GamingCafe.ClientAgent.Authentication;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Messaging;
using GamingCafe.ClientAgent.Security;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Communication;

public sealed class SignalRConnectionManager : ISignalRConnectionManager
{
    private readonly IConfigurationService _configService;
    private readonly IDeviceAuthenticationService _authService;
    private readonly ISecurityValidationService _securityValidationService;
    private readonly IConnectionStateManager _connectionStateManager;
    private readonly IReconnectionService _reconnectionService;
    private readonly IMessageHandler _messageHandler;
    private readonly ILogger<SignalRConnectionManager> _logger;

    private HubConnection? _hubConnection;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _isDisposed;

    public SignalRConnectionManager(
        IConfigurationService configService,
        IDeviceAuthenticationService authService,
        ISecurityValidationService securityValidationService,
        IConnectionStateManager connectionStateManager,
        IReconnectionService reconnectionService,
        IMessageHandler messageHandler,
        ILogger<SignalRConnectionManager> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _securityValidationService = securityValidationService ?? throw new ArgumentNullException(nameof(securityValidationService));
        _connectionStateManager = connectionStateManager ?? throw new ArgumentNullException(nameof(connectionStateManager));
        _reconnectionService = reconnectionService ?? throw new ArgumentNullException(nameof(reconnectionService));
        _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                _logger.LogInformation("SignalR connection is already active.");
                return;
            }

            var config = _configService.CurrentConfiguration;
            if (!_securityValidationService.ValidateHubUrl(config.Backend.HubUrl, out var error))
            {
                throw new InvalidOperationException($"Security validation rejected HubUrl: {error}");
            }

            _logger.LogDebug("Building SignalR connection to '{HubUrl}' with mandatory WebSocket transport...",
                config.Backend.HubUrl);

            _hubConnection = BuildHubConnection(config);
            RegisterHubEvents(_hubConnection);

            _connectionStateManager.SetStatus(AgentConnectionStatus.Connecting, "Initiating connection");

            await _hubConnection.StartAsync(cancellationToken).ConfigureAwait(false);
            _connectionStateManager.SetStatus(AgentConnectionStatus.Connected, "Connected to backend hub");
            _logger.LogInformation("SignalR WebSocket connection established successfully with backend '{HubUrl}'.", config.Backend.HubUrl);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _connectionStateManager.SetStatus(AgentConnectionStatus.Disconnected, "Connection cancelled during shutdown");
            _logger.LogInformation("SignalR connection attempt cancelled during graceful shutdown.");
            throw;
        }
        catch (Exception ex)
        {
            _connectionStateManager.SetStatus(AgentConnectionStatus.Disconnected, ex.Message);
            _logger.LogDebug(ex, "Detailed failure starting SignalR connection to '{HubUrl}':", _configService.CurrentConfiguration.Backend.HubUrl);
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_hubConnection != null)
            {
                _logger.LogInformation("Stopping SignalR connection...");
                await _hubConnection.StopAsync(cancellationToken).ConfigureAwait(false);
                _connectionStateManager.SetStatus(AgentConnectionStatus.Disconnected, "Graceful stop requested");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("SignalR connection stop completed on cancellation.");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Detailed exception while stopping SignalR connection.");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task SendAsync(string methodName, object? arg1, CancellationToken cancellationToken = default)
    {
        if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("Cannot send message: SignalR is not connected.");
        }

        await _hubConnection.SendAsync(methodName, arg1, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> InvokeAsync<T>(string methodName, object? arg1, CancellationToken cancellationToken = default)
    {
        if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("Cannot invoke method: SignalR is not connected.");
        }

        return await _hubConnection.InvokeAsync<T>(methodName, arg1, cancellationToken).ConfigureAwait(false);
    }

    private HubConnection BuildHubConnection(AgentConfiguration config)
    {
        var targetUrl = config.Backend.HubUrl;
        if (targetUrl.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
        {
            targetUrl = "https://" + targetUrl[6..];
        }
        else if (targetUrl.StartsWith("ws://", StringComparison.OrdinalIgnoreCase))
        {
            targetUrl = "http://" + targetUrl[5..];
        }

        var builder = new HubConnectionBuilder()
            .WithUrl(targetUrl, options =>
            {
                // MANDATORY: WebSockets transport strictly enforced. No LongPolling, no SSE fallback.
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = false;

                // Supply JWT bearer token dynamically
                options.AccessTokenProvider = () => _authService.GetAccessTokenAsync();

                // Configure TLS certificate validation
                options.HttpMessageHandlerFactory = handler =>
                {
                    if (handler is HttpClientHandler clientHandler)
                    {
                        clientHandler.ServerCertificateCustomValidationCallback =
                            (msg, cert, chain, errors) => _securityValidationService.ValidateServerCertificate(msg, cert, chain, errors);
                    }
                    return handler;
                };

                // WebSocket options for TLS certificate validation
                options.WebSocketConfiguration = wsOptions =>
                {
                    wsOptions.RemoteCertificateValidationCallback =
                        (sender, cert, chain, errors) =>
                        {
                            var cert2 = cert as System.Security.Cryptography.X509Certificates.X509Certificate2;
                            return _securityValidationService.ValidateServerCertificate(null, cert2, chain, errors);
                        };
                };
            })
            .WithAutomaticReconnect(_reconnectionService);

        return builder.Build();
    }

    private void RegisterHubEvents(HubConnection hub)
    {
        var hubUrl = _configService.CurrentConfiguration.Backend.HubUrl;

        hub.Reconnecting += error =>
        {
            _connectionStateManager.SetStatus(AgentConnectionStatus.Reconnecting, error?.Message);
            _connectionStateManager.IncrementReconnectAttempts();

            var (category, reason) = ConnectionErrorClassifier.Classify(error);
            _logger.LogWarning("SignalR connection lost to '{HubUrl}' [{Category}]: {Reason}. Automatic reconnecting...",
                hubUrl, category, reason);

            if (error != null)
            {
                _logger.LogDebug(error, "Detailed error during connection loss from '{HubUrl}':", hubUrl);
            }
            return Task.CompletedTask;
        };

        hub.Reconnected += connectionId =>
        {
            _connectionStateManager.SetStatus(AgentConnectionStatus.Connected, "Reconnected successfully");
            _logger.LogInformation("SignalR WebSocket connection restored successfully to '{HubUrl}'. ConnectionId: {ConnectionId}",
                hubUrl, connectionId);
            return Task.CompletedTask;
        };

        hub.Closed += error =>
        {
            _connectionStateManager.SetStatus(AgentConnectionStatus.Disconnected, error?.Message);
            if (error != null)
            {
                var (category, reason) = ConnectionErrorClassifier.Classify(error);
                _logger.LogWarning("SignalR connection closed to '{HubUrl}' [{Category}]: {Reason}",
                    hubUrl, category, reason);
                _logger.LogDebug(error, "Detailed error for closed connection to '{HubUrl}':", hubUrl);
            }
            else
            {
                _logger.LogInformation("SignalR connection closed gracefully.");
            }
            return Task.CompletedTask;
        };

        // Register protocol message listeners from Hub
        hub.On<DeviceApprovalMessage>("DeviceApproval", msg => _messageHandler.ProcessInboundMessageAsync(msg));
        hub.On<DeviceAuthenticationMessage>("DeviceAuthentication", msg => _messageHandler.ProcessInboundMessageAsync(msg));
        hub.On<HeartbeatAcknowledgment>("HeartbeatAck", msg => _messageHandler.ProcessInboundMessageAsync(msg));
        hub.On<DeviceConfigurationMessage>("DeviceConfiguration", msg => _messageHandler.ProcessInboundMessageAsync(msg));
        hub.On<DeviceRevocationMessage>("DeviceRevocation", msg => _messageHandler.ProcessInboundMessageAsync(msg));
        hub.On<ErrorMessage>("ErrorMessage", msg => _messageHandler.ProcessInboundMessageAsync(msg));
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync().ConfigureAwait(false);
            _hubConnection = null;
        }

        _connectionLock.Dispose();
    }
}
