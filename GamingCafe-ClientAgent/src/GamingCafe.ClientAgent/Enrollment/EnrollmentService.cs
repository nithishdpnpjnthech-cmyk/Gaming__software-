using System.Net;
using GamingCafe.ClientAgent.Authentication;
using GamingCafe.ClientAgent.Communication;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GamingCafe.ClientAgent.Enrollment;

/// <summary>
/// Contract for the device enrollment lifecycle orchestrator.
/// </summary>
public interface IEnrollmentService
{
    EnrollmentState CurrentState { get; }
    Task<EnrollmentState> EvaluateInitialStateAsync(DeviceIdentity identity, CancellationToken cancellationToken = default);
    Task<EnrollmentState> ProcessEnrollmentStepAsync(DeviceIdentity identity, CancellationToken cancellationToken = default);
    Task HandleRevocationAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Manages device registration, approval polling, credential acquisition, and revocation handling.
/// </summary>
public class EnrollmentService : IEnrollmentService
{
    private readonly IBackendApiClient _apiClient;
    private readonly IDeviceAuthenticationService _authService;
    private readonly IOptions<AgentConfiguration> _options;
    private readonly ILogger<EnrollmentService> _logger;

    private EnrollmentState _currentState = EnrollmentState.Unregistered;
    private readonly object _stateLock = new();

    public EnrollmentService(
        IBackendApiClient apiClient,
        IDeviceAuthenticationService authService,
        IOptions<AgentConfiguration> options,
        ILogger<EnrollmentService> logger)
    {
        _apiClient = apiClient;
        _authService = authService;
        _options = options;
        _logger = logger;
    }

    public EnrollmentState CurrentState
    {
        get
        {
            lock (_stateLock) return _currentState;
        }
    }

    public async Task<EnrollmentState> EvaluateInitialStateAsync(DeviceIdentity identity, CancellationToken cancellationToken = default)
    {
        var isAuthenticated = await _authService.IsAuthenticatedAsync(cancellationToken);
        if (isAuthenticated)
        {
            TransitionTo(EnrollmentState.Authenticated, "Found existing valid credentials. Device is authenticated.");
            return CurrentState;
        }

        TransitionTo(EnrollmentState.Unregistered, "No valid credentials found. Device requires initial enrollment.");
        return CurrentState;
    }

    public async Task<EnrollmentState> ProcessEnrollmentStepAsync(DeviceIdentity identity, CancellationToken cancellationToken = default)
    {
        var state = CurrentState;

        switch (state)
        {
            case EnrollmentState.Unregistered:
                return await RegisterDeviceAsync(identity, cancellationToken);

            case EnrollmentState.PendingApproval:
            case EnrollmentState.RegistrationPending:
                return await PollEnrollmentStatusAsync(identity, cancellationToken);

            case EnrollmentState.Approved:
                var secret = await _authService.GetDeviceSecretAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(secret))
                {
                    TransitionTo(EnrollmentState.Authenticated, "Device approved with credentials stored.");
                }
                else
                {
                    TransitionTo(EnrollmentState.PendingApproval, "Device marked approved but secret missing; re-polling.");
                }
                return CurrentState;

            case EnrollmentState.Authenticated:
                return CurrentState;

            case EnrollmentState.Rejected:
                _logger.LogWarning("Device enrollment is REJECTED by administrator.");
                return CurrentState;

            case EnrollmentState.Revoked:
                _logger.LogWarning("Device authorization is REVOKED by administrator.");
                return CurrentState;

            case EnrollmentState.Error:
            case EnrollmentState.Retrying:
                var hasCreds = await _authService.IsAuthenticatedAsync(cancellationToken);
                if (hasCreds)
                {
                    TransitionTo(EnrollmentState.Authenticated, "Resuming Authenticated state after transient error.");
                }
                else
                {
                    TransitionTo(EnrollmentState.Unregistered, "Retrying registration after error.");
                }
                return CurrentState;

            default:
                _logger.LogWarning("Unknown enrollment state {State}. Resetting to Unregistered.", state);
                TransitionTo(EnrollmentState.Unregistered, "Reset from unknown state.");
                return CurrentState;
        }
    }

    public async Task HandleRevocationAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Device revocation triggered. Purging local credentials.");
        await _authService.RevokeCredentialsAsync(cancellationToken);
        TransitionTo(EnrollmentState.Revoked, "Device authorization revoked by server.");
    }

    private async Task<EnrollmentState> RegisterDeviceAsync(DeviceIdentity identity, CancellationToken cancellationToken)
    {
        TransitionTo(EnrollmentState.RegistrationPending, "Sending registration request.");

        var request = new DeviceRegistrationRequest
        {
            DeviceId = identity.DeviceId,
            Hostname = identity.Hostname,
            OperatingSystem = identity.OperatingSystem,
            AgentVersion = _options.Value.AgentVersion,
            ClientTimestampUtc = DateTime.UtcNow,
            HardwareDigest = identity.HardwareDigest
        };

        var response = await _apiClient.RegisterDeviceAsync(request, cancellationToken);

        if (!response.IsSuccess || response.Data == null)
        {
            _logger.LogWarning("Registration failed: {ErrorMessage}. HTTP {StatusCode}",
                response.ErrorMessage, response.StatusCode);

            TransitionTo(EnrollmentState.Error, $"Registration error: {response.ErrorMessage}");
            return CurrentState;
        }

        var data = response.Data;
        _logger.LogInformation("Registration response: Status={Status}, Message={Message}", data.Status, data.Message);

        if (string.Equals(data.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(data.DeviceSecret))
            {
                await _authService.StoreIssuedSecretAsync(data.DeviceSecret, cancellationToken);
                TransitionTo(EnrollmentState.Authenticated, "Device automatically approved with credentials.");
            }
            else
            {
                TransitionTo(EnrollmentState.Approved, "Device approved; awaiting token exchange.");
            }
        }
        else if (string.Equals(data.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            TransitionTo(EnrollmentState.Rejected, $"Registration rejected: {data.Message}");
        }
        else
        {
            TransitionTo(EnrollmentState.PendingApproval, "Registration acknowledged; awaiting admin approval.");
        }

        return CurrentState;
    }

    private async Task<EnrollmentState> PollEnrollmentStatusAsync(DeviceIdentity identity, CancellationToken cancellationToken)
    {
        var response = await _apiClient.GetEnrollmentStatusAsync(identity.DeviceId, cancellationToken);

        if (!response.IsSuccess || response.Data == null)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Device ID not found on backend. Resetting to Unregistered.");
                TransitionTo(EnrollmentState.Unregistered, "Device not found on server.");
                return CurrentState;
            }

            _logger.LogWarning("Status polling failed: {ErrorMessage}. Will retry with backoff.", response.ErrorMessage);
            TransitionTo(EnrollmentState.Retrying, "Polling retry backoff.");
            return CurrentState;
        }

        var data = response.Data;
        _logger.LogInformation("Enrollment status update: Status={Status}", data.Status);

        if (string.Equals(data.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(data.DeviceSecret))
            {
                await _authService.StoreIssuedSecretAsync(data.DeviceSecret, cancellationToken);
                TransitionTo(EnrollmentState.Authenticated, "Device approved by administrator.");
            }
            else
            {
                TransitionTo(EnrollmentState.Approved, "Device approved; credentials pending.");
            }
        }
        else if (string.Equals(data.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            await _authService.RevokeCredentialsAsync(cancellationToken);
            TransitionTo(EnrollmentState.Rejected, "Enrollment was explicitly rejected by cafe administrator.");
        }
        else if (string.Equals(data.Status, "Revoked", StringComparison.OrdinalIgnoreCase))
        {
            await HandleRevocationAsync(cancellationToken);
        }
        else
        {
            TransitionTo(EnrollmentState.PendingApproval, "Device remains pending admin approval.");
        }

        return CurrentState;
    }

    private void TransitionTo(EnrollmentState newState, string reason)
    {
        lock (_stateLock)
        {
            if (_currentState != newState)
            {
                _logger.LogInformation("Enrollment State Change: [{OldState}] -> [{NewState}]. Reason: {Reason}",
                    _currentState, newState, reason);
                _currentState = newState;
            }
        }
    }
}
