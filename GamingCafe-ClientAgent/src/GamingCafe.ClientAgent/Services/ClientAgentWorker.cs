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
    private readonly SemaphoreSlim _connectLoopLock = new(1, 1);

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
            _logger.LogInformation("Agent initialized from storage '{StoragePath}'. DeviceId: {DeviceId}, BackendDeviceId: {BackendId}, DeviceCode: '{Code}', Status: '{Status}', Host: '{Host}', Version: '{Version}'",
                _identityStore.IdentityFilePath, identity.DeviceId, identity.BackendDeviceId, identity.DeviceCode, identity.RegistrationStatus, identity.DeviceName, identity.ClientVersion);

            // Optional diagnostic validation against expected DeviceId
            var expectedDeviceIdEnv = Environment.GetEnvironmentVariable("EXPECTED_DEVICE_ID");
            if (!string.IsNullOrWhiteSpace(expectedDeviceIdEnv) && Guid.TryParse(expectedDeviceIdEnv, out var expectedGuid))
            {
                if (identity.DeviceId == expectedGuid)
                {
                    _logger.LogInformation("DIAGNOSTIC CHECK PASSED: Loaded DeviceId matches EXPECTED_DEVICE_ID '{ExpectedId}'.", expectedGuid);
                }
                else
                {
                    _logger.LogWarning("DIAGNOSTIC CHECK WARNING: Loaded DeviceId '{ActualId}' differs from EXPECTED_DEVICE_ID '{ExpectedId}'! Source File: '{SourceFile}'",
                        identity.DeviceId, expectedGuid, _identityStore.IdentityFilePath);
                }
            }

            // 2. Initialize enrollment state
            await _enrollmentService.InitializeStateAsync().ConfigureAwait(false);

            // 3. Register message dispatch handlers
            RegisterProtocolHandlers();

            // 4. Perform Backend REST Device Registration if not already registered
            await PerformDeviceRegistrationAsync(stoppingToken).ConfigureAwait(false);

            // 5. Connect to SignalR Backend Hub if enabled
            if (_configService.CurrentConfiguration.Backend.EnableSignalR)
            {
                await ConnectWithRetryLoopAsync(stoppingToken).ConfigureAwait(false);
            }
            else
            {
                _logger.LogInformation("SignalR duplex channel paused: Backend SignalR Hub is not deployed. Agent operating in standalone REST registration and local telemetry mode.");
            }

            // 6. Main execution loop
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
                    else if (_enrollmentService.CurrentState == EnrollmentState.PendingApproval)
                    {
                        _logger.LogDebug("Device {DeviceId} (Code: {Code}) is awaiting administrator approval in backend dashboard.",
                            identity.DeviceId, identity.DeviceCode);
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
            _connectLoopLock.Dispose();
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
        if (!await _connectLoopLock.WaitAsync(0, stoppingToken).ConfigureAwait(false))
        {
            _logger.LogDebug("Connection retry loop is already actively running. Skipping duplicate concurrent loop.");
            return;
        }

        try
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

                    if (category == ConnectionFailureCategory.AuthenticationFailure)
                    {
                        var currentState = _enrollmentService.CurrentState;
                        _logger.LogWarning(
                            "SignalR Hub rejected connection with HTTP 401 Unauthorized. CurrentEnrollmentState={State}. Server requires valid device authentication token before granting WebSocket access.",
                            currentState);
                    }

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
        finally
        {
            _connectLoopLock.Release();
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
                var acked = await _heartbeatService.SendHeartbeatAsync(stoppingToken).ConfigureAwait(false);
                if (!acked)
                {
                    _logger.LogWarning("Heartbeat cycle completed without server acknowledgment.");
                }
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

        // Verify existing registration if BackendDeviceId is present
        if (identity.BackendDeviceId.HasValue)
        {
            _logger.LogInformation("Device {DeviceId} has existing BackendDeviceId '{BackendId}' (Status: {Status}). Verifying with backend...",
                identity.DeviceId, identity.BackendDeviceId, identity.RegistrationStatus);

            var verifyResult = await _backendApiClient.VerifyDeviceExistsAsync(identity.BackendDeviceId.Value, stoppingToken).ConfigureAwait(false);

            if (verifyResult.Status == DeviceVerificationStatus.NotFound)
            {
                // The backend database was wiped or this device was deleted on the server!
                // Clear the stale BackendDeviceId and safely recover via re-registration workflow.
                _logger.LogWarning("Existing backend record '{BackendId}' was NOT found on backend (HTTP 404). Device was deleted or backend DB was reset. Clearing stale BackendDeviceId and initiating registration recovery...",
                    identity.BackendDeviceId.Value);

                identity.BackendDeviceId = null;
                identity.RegistrationStatus = "UNREGISTERED";
                identity.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
                _identityStore.SaveIdentity(identity);

                await _enrollmentService.TransitionToAsync(EnrollmentState.Unregistered, "Stale BackendDeviceId cleared; initiating re-registration.").ConfigureAwait(false);
            }
            else
            {
                // In Confirmed, Unauthorized (401/403 - auth policy), ServerError, or NetworkFailure:
                // Do NOT clear registration because of HTTP 401, 403, timeout or server errors.
                _logger.LogInformation("Existing backend registration check outcome: {Status}. Preserving local identity and registration state.",
                    verifyResult.Status);
                return;
            }
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
}
