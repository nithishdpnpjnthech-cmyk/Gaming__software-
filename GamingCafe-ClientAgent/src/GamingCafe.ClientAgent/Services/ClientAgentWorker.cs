using GamingCafe.ClientAgent.Communication;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Enrollment;
using GamingCafe.ClientAgent.Heartbeat;
using GamingCafe.ClientAgent.Identity;
using GamingCafe.ClientAgent.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Services;

public sealed class ClientAgentWorker : BackgroundService
{
    private readonly ISignalRConnectionManager _connectionManager;
    private readonly IDeviceEnrollmentService _enrollmentService;
    private readonly IDeviceIdentityService _identityService;
    private readonly IHeartbeatService _heartbeatService;
    private readonly IConnectionStateManager _connectionStateManager;
    private readonly IConfigurationService _configService;
    private readonly IReconnectionService _reconnectionService;
    private readonly IMessageHandler _messageHandler;
    private readonly ILogger<ClientAgentWorker> _logger;

    public ClientAgentWorker(
        ISignalRConnectionManager connectionManager,
        IDeviceEnrollmentService enrollmentService,
        IDeviceIdentityService identityService,
        IHeartbeatService heartbeatService,
        IConnectionStateManager connectionStateManager,
        IConfigurationService configService,
        IReconnectionService reconnectionService,
        IMessageHandler messageHandler,
        ILogger<ClientAgentWorker> logger)
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _enrollmentService = enrollmentService ?? throw new ArgumentNullException(nameof(enrollmentService));
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
        _heartbeatService = heartbeatService ?? throw new ArgumentNullException(nameof(heartbeatService));
        _connectionStateManager = connectionStateManager ?? throw new ArgumentNullException(nameof(connectionStateManager));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _reconnectionService = reconnectionService ?? throw new ArgumentNullException(nameof(reconnectionService));
        _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Gaming Cafe Client Agent starting up as Windows Service...");

        try
        {
            // 1. Initialize local identity
            var identity = _identityService.GetOrCreateIdentity();
            _logger.LogInformation("Agent initialized for DeviceId: {DeviceId}, Host: {Host}, Version: {Version}",
                identity.DeviceId, identity.DeviceName, identity.ClientVersion);

            // 2. Initialize enrollment state
            await _enrollmentService.InitializeStateAsync().ConfigureAwait(false);

            // 3. Register message dispatch handlers
            RegisterProtocolHandlers();

            // 4. Connect to SignalR Backend Hub over WebSocket Secure (WSS)
            await ConnectWithRetryLoopAsync(stoppingToken).ConfigureAwait(false);

            // 5. Main execution loop
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var intervalSeconds = _configService.CurrentConfiguration.Heartbeat.IntervalSeconds;
                    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken).ConfigureAwait(false);

                    // If connected, perform state actions
                    if (_connectionManager.IsConnected)
                    {
                        await PerformConnectedActionsAsync(stoppingToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in agent execution cycle.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown requested (Ctrl+C or Service Stop)
        }
        finally
        {
            _logger.LogInformation("Gaming Cafe Client Agent service shutting down gracefully.");
            await _connectionManager.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private void RegisterProtocolHandlers()
    {
        _messageHandler.RegisterHandler<DeviceApprovalMessage>("DeviceApproval", async msg =>
        {
            await _enrollmentService.HandleApprovalMessageAsync(msg).ConfigureAwait(false);
        });

        _messageHandler.RegisterHandler<DeviceAuthenticationMessage>("DeviceAuthentication", async msg =>
        {
            await _enrollmentService.HandleAuthenticationMessageAsync(msg).ConfigureAwait(false);
        });

        _messageHandler.RegisterHandler<HeartbeatAcknowledgment>("HeartbeatAck", msg =>
        {
            _heartbeatService.HandleAcknowledgment(msg);
            return Task.CompletedTask;
        });

        _messageHandler.RegisterHandler<DeviceRevocationMessage>("DeviceRevocation", async msg =>
        {
            await _enrollmentService.HandleRevocationMessageAsync(msg).ConfigureAwait(false);
        });

        _messageHandler.RegisterHandler<DeviceConfigurationMessage>("DeviceConfiguration", msg =>
        {
            _logger.LogInformation("Received dynamic configuration update (Version: {Version})", msg.ConfigVersion);
            return Task.CompletedTask;
        });
    }

    private async Task ConnectWithRetryLoopAsync(CancellationToken stoppingToken)
    {
        var attempt = 0;
        var hubUrl = _configService.CurrentConfiguration.Backend.HubUrl;

        while (!stoppingToken.IsCancellationRequested)
        {
            attempt++;
            try
            {
                _logger.LogInformation("Attempting to connect to backend via WSS: '{HubUrl}'...", hubUrl);
                await _connectionManager.StartAsync(stoppingToken).ConfigureAwait(false);
                _logger.LogInformation("SignalR WebSocket connection established successfully with backend: '{HubUrl}'", hubUrl);

                // Check registration immediately upon connect
                await CheckAndSendRegistrationAsync(stoppingToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Initial connection attempt cancelled due to service shutdown.");
                return;
            }
            catch (Exception ex)
            {
                var (category, reason) = ConnectionErrorClassifier.Classify(ex);
                var delay = _reconnectionService.CalculateNextDelay(attempt);

                _logger.LogWarning("Connection to backend '{HubUrl}' failed [{Category}]: {Reason}. Retrying in {Delay:N1}s (attempt {Attempt})...",
                    hubUrl, category, reason, delay.TotalSeconds, attempt);

                _logger.LogDebug(ex, "Detailed connection failure exception for '{HubUrl}':", hubUrl);

                try
                {
                    await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Retry delay cancelled due to service shutdown.");
                    return;
                }
            }
        }
    }

    private async Task PerformConnectedActionsAsync(CancellationToken stoppingToken)
    {
        var state = _enrollmentService.CurrentState;

        switch (state)
        {
            case EnrollmentState.Unregistered:
                await CheckAndSendRegistrationAsync(stoppingToken).ConfigureAwait(false);
                break;

            case EnrollmentState.PendingApproval:
                _logger.LogDebug("Device is pending administrator approval. Heartbeat suspended.");
                break;

            case EnrollmentState.Approved:
            case EnrollmentState.Authenticated:
                await _heartbeatService.SendHeartbeatAsync(stoppingToken).ConfigureAwait(false);
                break;

            case EnrollmentState.Rejected:
                _logger.LogWarning("Device enrollment was rejected. Contact administrator.");
                break;

            case EnrollmentState.Revoked:
                _logger.LogCritical("Device enrollment is revoked. Halting operations.");
                break;
        }
    }

    private async Task CheckAndSendRegistrationAsync(CancellationToken stoppingToken)
    {
        if (_enrollmentService.CurrentState == EnrollmentState.Unregistered)
        {
            var identity = _identityService.CurrentIdentity;
            _logger.LogInformation("Sending device registration message to hub for {DeviceId} ({DeviceName})...",
                identity.DeviceId, identity.DeviceName);

            var regMsg = new DeviceRegistrationMessage
            {
                DeviceId = identity.DeviceId,
                DeviceName = identity.DeviceName,
                HardwareIdentifier = identity.HardwareIdentifier,
                MacAddress = identity.MacAddress,
                OsVersion = identity.OsVersion,
                ClientVersion = identity.ClientVersion
            };

            await _connectionManager.SendAsync("RegisterDevice", regMsg, stoppingToken).ConfigureAwait(false);
            await _enrollmentService.TransitionToAsync(EnrollmentState.PendingApproval, "Registration request sent to backend.").ConfigureAwait(false);
        }
    }
}
