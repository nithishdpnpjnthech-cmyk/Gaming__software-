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
    private readonly IBackendApiClient _backendApiClient;
    private readonly IDeviceEnrollmentService _enrollmentService;
    private readonly IDeviceIdentityService _identityService;
    private readonly IDeviceIdentityStore _identityStore;
    private readonly IHeartbeatService _heartbeatService;
    private readonly IConnectionStateManager _connectionStateManager;
    private readonly IConfigurationService _configService;
    private readonly IReconnectionService _reconnectionService;
    private readonly IMessageHandler _messageHandler;
    private readonly ILogger<ClientAgentWorker> _logger;

    public ClientAgentWorker(
        ISignalRConnectionManager connectionManager,
        IBackendApiClient backendApiClient,
        IDeviceEnrollmentService enrollmentService,
        IDeviceIdentityService identityService,
        IDeviceIdentityStore identityStore,
        IHeartbeatService heartbeatService,
        IConnectionStateManager connectionStateManager,
        IConfigurationService configService,
        IReconnectionService reconnectionService,
        IMessageHandler messageHandler,
        ILogger<ClientAgentWorker> logger)
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _backendApiClient = backendApiClient ?? throw new ArgumentNullException(nameof(backendApiClient));
        _enrollmentService = enrollmentService ?? throw new ArgumentNullException(nameof(enrollmentService));
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
        _identityStore = identityStore ?? throw new ArgumentNullException(nameof(identityStore));
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

        var config = _configService.CurrentConfiguration;
        _logger.LogInformation("Loaded Runtime Configuration: ApiBaseUrl='{ApiBaseUrl}', HubUrl='{HubUrl}', EnableSignalR={EnableSignalR}, RequireWss={RequireWss}, RejectNonWss={RejectNonWss}, StorageDirectory='{StorageDir}', HttpTimeout={HttpTimeout}s",
            config.Backend.ApiBaseUrl, config.Backend.HubUrl, config.Backend.EnableSignalR, config.Backend.RequireWss, config.Security.RejectNonWss, config.Identity.StorageDirectory, config.Backend.HttpTimeoutSeconds);

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

            // 4. Perform Backend REST Device Registration if not already registered
            await PerformDeviceRegistrationAsync(stoppingToken).ConfigureAwait(false);

            // 5. Check if device is approved and authenticate
            if (_enrollmentService.CurrentState != EnrollmentState.Authenticated)
            {
                await CheckApprovalAndAuthenticateAsync(stoppingToken).ConfigureAwait(false);
            }

            // 6. Connect to SignalR Backend Hub if enabled AND authenticated
            if (_configService.CurrentConfiguration.Backend.EnableSignalR &&
                _enrollmentService.CurrentState == EnrollmentState.Authenticated)
            {
                await ConnectWithRetryLoopAsync(stoppingToken).ConfigureAwait(false);
            }
            else if (_enrollmentService.CurrentState != EnrollmentState.Authenticated)
            {
                _logger.LogInformation("SignalR connection deferred: Device registration is awaiting administrator approval. Polling for approval...");
            }
            else
            {
                _logger.LogInformation("SignalR duplex channel paused: Backend SignalR Hub is not deployed. Agent operating in standalone REST registration and local telemetry mode.");
            }

            // 7. Main execution loop
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var intervalSeconds = _configService.CurrentConfiguration.Heartbeat.IntervalSeconds;
                    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken).ConfigureAwait(false);

                    // If connected to SignalR Hub, dispatch actions
                    if (_connectionManager.IsConnected)
                    {
                        await PerformConnectedActionsAsync(stoppingToken).ConfigureAwait(false);
                    }
                    else if (_enrollmentService.CurrentState != EnrollmentState.Authenticated)
                    {
                        await CheckApprovalAndAuthenticateAsync(stoppingToken).ConfigureAwait(false);
                    }
                    else if (_configService.CurrentConfiguration.Backend.EnableSignalR)
                    {
                        await ConnectWithRetryLoopAsync(stoppingToken).ConfigureAwait(false);
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
            if (_configService.CurrentConfiguration.Backend.EnableSignalR)
            {
                await _connectionManager.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
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
            _logger.LogCritical("Received remote revocation directive from backend. Inactivating credentials and disconnecting.");
            await _enrollmentService.HandleRevocationMessageAsync(msg).ConfigureAwait(false);
            if (_connectionManager.IsConnected)
            {
                await _connectionManager.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
        });

        _messageHandler.RegisterHandler<DeviceErasedMessage>("DeviceErased", async msg =>
        {
            _logger.LogCritical("Received DeviceErased directive from backend: '{Message}'. Resetting identity and initiating clean re-registration.", msg.Message);
            await HandleDeviceErasedAsync(CancellationToken.None).ConfigureAwait(false);
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
                await PerformDeviceRegistrationAsync(stoppingToken).ConfigureAwait(false);
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
                await PerformDeviceRegistrationAsync(stoppingToken).ConfigureAwait(false);
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

    private async Task PerformDeviceRegistrationAsync(CancellationToken stoppingToken)
    {
        var identity = _identityService.CurrentIdentity;

        // If already registered with backend and has a confirmed BackendDeviceId, skip initial registration call
        if (identity.BackendDeviceId.HasValue)
        {
            _logger.LogInformation("Device {DeviceId} is already registered with backend (Backend DeviceId: {BackendId}, Status: {Status}).",
                identity.DeviceId, identity.BackendDeviceId, identity.RegistrationStatus);
            return;
        }

        var attempt = 0;
        var request = new DeviceRegistrationRequestDto
        {
            DeviceCode = string.IsNullOrWhiteSpace(identity.DeviceCode) 
                ? "PC-" + identity.DeviceId.ToString("N")[..8].ToUpperInvariant() 
                : identity.DeviceCode,
            DisplayName = string.IsNullOrWhiteSpace(identity.DisplayName) 
                ? identity.DeviceName 
                : identity.DisplayName,
            Hostname = identity.DeviceName,
            HardwareFingerprintHash = identity.HardwareIdentifier,
            AgentVersion = identity.ClientVersion
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            attempt++;
            try
            {
                _logger.LogInformation("Attempting device registration with backend at '{ApiUrl}' (attempt {Attempt})...",
                    _configService.CurrentConfiguration.Backend.ApiBaseUrl, attempt);

                var result = await _backendApiClient.RegisterDeviceAsync(request, stoppingToken).ConfigureAwait(false);

                if (result.Status == RegistrationResultStatus.Success)
                {
                    identity.BackendDeviceId = result.Data!.Id;
                    identity.RegistrationStatus = result.Data.RegistrationStatus; // "PENDING"
                    identity.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
                    _identityStore.SaveIdentity(identity);

                    await _enrollmentService.TransitionToAsync(
                        EnrollmentState.PendingApproval, 
                        $"Registered successfully with backend ID {result.Data.Id}.").ConfigureAwait(false);

                    _logger.LogInformation("Device registration complete! Awaiting admin approval. Backend DeviceId: {Id}", result.Data.Id);
                    return;
                }

                if (result.Status == RegistrationResultStatus.AlreadyExists)
                {
                    // Strict rule: Do NOT assume a 409 response proves that the existing device belongs to this Agent.
                    // The backend does not return a device ID or proof of ownership on 409.
                    // We must NOT treat 409 Conflict as approval, authentication, or automatic ownership.
                    _logger.LogError("Registration conflict (HTTP 409 Conflict): Device code '{DeviceCode}' already exists on backend, but this agent has no confirmed BackendDeviceId. Cannot assume existing registration belongs to this agent. Halting automatic registration until device code is changed or existing device is resolved in backend.",
                        request.DeviceCode);

                    identity.RegistrationStatus = "CONFLICT";
                    identity.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
                    _identityStore.SaveIdentity(identity);

                    await _enrollmentService.TransitionToAsync(
                        EnrollmentState.Error, 
                        $"Device code '{request.DeviceCode}' conflicts with existing backend record. Cannot assume ownership.").ConfigureAwait(false);
                    return;
                }

                if (result.Status == RegistrationResultStatus.ValidationError)
                {
                    _logger.LogError("Backend rejected device registration with validation error (HTTP 400): {Error}",
                        result.ErrorMessage);
                    return;
                }

                // For NetworkFailure or ServerError, retry with exponential backoff
                var delay = _reconnectionService.CalculateNextDelay(attempt);
                _logger.LogWarning("Device registration attempt failed [{Status}]: {Error}. Retrying in {Delay:N1}s (attempt {Attempt})...",
                    result.Status, result.ErrorMessage, delay.TotalSeconds, attempt);

                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Registration loop cancelled due to service shutdown.");
                return;
            }
            catch (Exception ex)
            {
                var (category, reason) = ConnectionErrorClassifier.Classify(ex);
                var delay = _reconnectionService.CalculateNextDelay(attempt);

                _logger.LogWarning("Registration attempt threw exception [{Category}]: {Reason}. Retrying in {Delay:N1}s (attempt {Attempt})...",
                    category, reason, delay.TotalSeconds, attempt);
                _logger.LogDebug(ex, "Detailed exception during registration retry loop:");

                try
                {
                    await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }

    private async Task CheckApprovalAndAuthenticateAsync(CancellationToken stoppingToken)
    {
        var identity = _identityService.CurrentIdentity;
        var deviceCode = string.IsNullOrWhiteSpace(identity.DeviceCode)
            ? "PC-" + identity.DeviceId.ToString("N")[..8].ToUpperInvariant()
            : identity.DeviceCode;

        var request = new DeviceAuthRequestDto
        {
            DeviceCode = deviceCode,
            HardwareFingerprintHash = identity.HardwareIdentifier
        };

        try
        {
            _logger.LogDebug("Checking approval status and requesting authentication token for Device {DeviceCode}...", deviceCode);
            var result = await _backendApiClient.AuthenticateDeviceAsync(request, stoppingToken).ConfigureAwait(false);

            if (result.Status == DeviceAuthResultStatus.Success && result.Data != null)
            {
                _logger.LogInformation("Device {DeviceCode} is APPROVED! Received authentication token (valid until {ExpiresAt}).",
                    deviceCode, result.Data.ExpiresAt);

                // Save encrypted via DPAPI and transition state to Authenticated
                await _enrollmentService.HandleAuthenticationMessageAsync(new DeviceAuthenticationMessage
                {
                    DeviceId = result.Data.DeviceId,
                    AccessToken = result.Data.AccessToken,
                    TokenExpiresAtUtc = result.Data.ExpiresAt
                }).ConfigureAwait(false);

                identity.RegistrationStatus = "APPROVED";
                identity.BackendDeviceId = result.Data.DeviceId;
                identity.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
                _identityStore.SaveIdentity(identity);

                // Connect to SignalR if enabled and not already connected
                if (_configService.CurrentConfiguration.Backend.EnableSignalR && !_connectionManager.IsConnected)
                {
                    await ConnectWithRetryLoopAsync(stoppingToken).ConfigureAwait(false);
                }
            }
            else if (result.Status == DeviceAuthResultStatus.PendingApproval)
            {
                _logger.LogDebug("Device {DeviceCode} is awaiting administrator approval in backend.", deviceCode);
            }
            else if (result.Status == DeviceAuthResultStatus.NotFound)
            {
                _logger.LogWarning("Device {DeviceCode} was completely erased from backend database. Resetting identity and initiating clean re-registration...", deviceCode);
                await HandleDeviceErasedAsync(stoppingToken).ConfigureAwait(false);
            }
            else if (result.Status == DeviceAuthResultStatus.Denied)
            {
                _logger.LogWarning("Device authentication was DENIED for {DeviceCode}: {Error}", deviceCode, result.ErrorMessage);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Exception during periodic device authentication check for {DeviceCode}.", deviceCode);
        }
    }

    private async Task HandleDeviceErasedAsync(CancellationToken stoppingToken)
    {
        var identity = _identityService.CurrentIdentity;
        identity.BackendDeviceId = null;
        identity.RegistrationStatus = "UNREGISTERED";
        identity.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        _identityStore.SaveIdentity(identity);

        await _enrollmentService.HandleRevocationMessageAsync(new DeviceRevocationMessage
        {
            Reason = "Device was erased completely from backend database."
        }).ConfigureAwait(false);

        if (_connectionManager.IsConnected)
        {
            await _connectionManager.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }

        await _enrollmentService.TransitionToAsync(
            EnrollmentState.Unregistered,
            "Device was erased from backend database. Ready for re-registration.").ConfigureAwait(false);

        // Immediately trigger clean device registration
        await PerformDeviceRegistrationAsync(stoppingToken).ConfigureAwait(false);
    }
}
