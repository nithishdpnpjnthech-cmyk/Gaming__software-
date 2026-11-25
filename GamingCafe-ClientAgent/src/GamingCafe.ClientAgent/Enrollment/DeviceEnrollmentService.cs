using GamingCafe.ClientAgent.Authentication;
using GamingCafe.ClientAgent.Identity;
using GamingCafe.ClientAgent.Messaging;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Enrollment;

public sealed class DeviceEnrollmentService : IDeviceEnrollmentService
{
    private readonly IDeviceIdentityService _identityService;
    private readonly IDeviceIdentityStore _identityStore;
    private readonly IDeviceAuthenticationService _authService;
    private readonly ILogger<DeviceEnrollmentService> _logger;
    private EnrollmentState _currentState = EnrollmentState.Unregistered;
    private readonly object _stateLock = new();

    public event Action<EnrollmentState>? StateChanged;

    public DeviceEnrollmentService(
        IDeviceIdentityService identityService,
        IDeviceIdentityStore identityStore,
        IDeviceAuthenticationService authService,
        ILogger<DeviceEnrollmentService> logger)
    {
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
        _identityStore = identityStore ?? throw new ArgumentNullException(nameof(identityStore));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public EnrollmentState CurrentState
    {
        get
        {
            lock (_stateLock) return _currentState;
        }
    }

    public Task InitializeStateAsync()
    {
        lock (_stateLock)
        {
            var identity = _identityService.GetOrCreateIdentity();
            var creds = _authService.CurrentCredentials;

            if (creds != null && creds.HasValidToken)
            {
                _currentState = EnrollmentState.Authenticated;
                _logger.LogInformation("Device {DeviceId} (Backend ID: {BackendId}) already enrolled and authenticated.",
                    identity.DeviceId, identity.BackendDeviceId);
            }
            else if (creds != null || string.Equals(identity.RegistrationStatus, "APPROVED", StringComparison.OrdinalIgnoreCase))
            {
                _currentState = EnrollmentState.Approved;
                _logger.LogInformation("Device {DeviceId} (Backend ID: {BackendId}) is approved (token required: {TokenNeeded}).",
                    identity.DeviceId, identity.BackendDeviceId, creds == null);
            }
            else if (string.Equals(identity.RegistrationStatus, "REJECTED", StringComparison.OrdinalIgnoreCase))
            {
                _currentState = EnrollmentState.Rejected;
                _logger.LogWarning("Device {DeviceId} (Backend ID: {BackendId}) was previously rejected by administrator.",
                    identity.DeviceId, identity.BackendDeviceId);
            }
            else if (string.Equals(identity.RegistrationStatus, "REVOKED", StringComparison.OrdinalIgnoreCase))
            {
                _currentState = EnrollmentState.Revoked;
                _logger.LogCritical("Device {DeviceId} (Backend ID: {BackendId}) was previously revoked.",
                    identity.DeviceId, identity.BackendDeviceId);
            }
            else if (string.Equals(identity.RegistrationStatus, "CONFLICT", StringComparison.OrdinalIgnoreCase))
            {
                _currentState = EnrollmentState.Error;
                _logger.LogError("Device {DeviceId} is in registration CONFLICT state.", identity.DeviceId);
            }
            else if (identity.BackendDeviceId.HasValue)
            {
                _currentState = EnrollmentState.PendingApproval;
                _logger.LogInformation("Device {DeviceId} (Code: {Code}) is registered with backend ID {BackendId} and awaiting administrator approval.",
                    identity.DeviceId, identity.DeviceCode, identity.BackendDeviceId);
            }
            else
            {
                _currentState = EnrollmentState.Unregistered;
                _logger.LogInformation("Device {DeviceId} is unregistered.", identity.DeviceId);
            }
        }

        return Task.CompletedTask;
    }

    public Task HandleApprovalMessageAsync(DeviceApprovalMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var identity = _identityService.CurrentIdentity;

        // CRITICAL: Strictly validate UUID against the Backend-generated Device ID
        // Never compare Backend Device UUID against Local Device UUID.
        if (identity.BackendDeviceId.HasValue)
        {
            if (message.DeviceId != identity.BackendDeviceId.Value)
            {
                _logger.LogWarning(
                    "Security validation rejected DeviceApproval: Mismatched Backend DeviceId. Expected '{ExpectedBackendId}', received '{ReceivedId}'. Message ignored.",
                    identity.BackendDeviceId.Value, message.DeviceId);
                return Task.CompletedTask;
            }
        }
        else
        {
            _logger.LogWarning(
                "Security validation rejected DeviceApproval: Agent has no registered BackendDeviceId. Cannot validate approval message for DeviceId '{ReceivedId}'.",
                message.DeviceId);
            return Task.CompletedTask;
        }

        if (message.IsApproved)
        {
            _logger.LogInformation("Device approval verified for Backend DeviceId '{BackendId}'. Zone assigned: {Zone}",
                identity.BackendDeviceId.Value, message.AssignedZone ?? "Default");

            // Persist approved state into device identity store so it survives restarts
            identity.RegistrationStatus = "APPROVED";
            identity.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
            _identityStore.SaveIdentity(identity);

            return TransitionToAsync(EnrollmentState.Approved, "Approved by administrator.");
        }
        else
        {
            _logger.LogWarning("Device enrollment rejected for Backend DeviceId '{BackendId}'. Reason: {Reason}",
                identity.BackendDeviceId.Value, message.RejectionReason ?? "None provided.");

            identity.RegistrationStatus = "REJECTED";
            identity.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
            _identityStore.SaveIdentity(identity);

            return TransitionToAsync(EnrollmentState.Rejected, message.RejectionReason);
        }
    }

    public Task HandleAuthenticationMessageAsync(DeviceAuthenticationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var identity = _identityService.CurrentIdentity;
        if (identity.BackendDeviceId.HasValue && message.DeviceId != identity.BackendDeviceId.Value)
        {
            _logger.LogWarning(
                "Security validation rejected DeviceAuthentication: Mismatched Backend DeviceId. Expected '{ExpectedBackendId}', received '{ReceivedId}'.",
                identity.BackendDeviceId.Value, message.DeviceId);
            return Task.CompletedTask;
        }

        var credentials = new DeviceCredentials
        {
            DeviceId = message.DeviceId,
            AccessToken = message.AccessToken,
            RefreshToken = message.RefreshToken,
            TokenExpiresAtUtc = message.TokenExpiresAtUtc,
            IssuedAtUtc = DateTimeOffset.UtcNow
        };

        _authService.SetCredentials(credentials);
        return TransitionToAsync(EnrollmentState.Authenticated, "Received authentication token.");
    }

    public Task HandleRevocationMessageAsync(DeviceRevocationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var identity = _identityService.CurrentIdentity;
        if (identity.BackendDeviceId.HasValue && message.DeviceId != identity.BackendDeviceId.Value)
        {
            _logger.LogWarning(
                "Security validation rejected DeviceRevocation: Mismatched Backend DeviceId. Expected '{ExpectedBackendId}', received '{ReceivedId}'.",
                identity.BackendDeviceId.Value, message.DeviceId);
            return Task.CompletedTask;
        }

        _logger.LogCritical("Device revocation verified for Backend DeviceId '{BackendId}'! Reason: {Reason}",
            identity.BackendDeviceId, message.Reason);

        identity.RegistrationStatus = "REVOKED";
        identity.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        _identityStore.SaveIdentity(identity);

        _authService.InvalidateCredentials();
        return TransitionToAsync(EnrollmentState.Revoked, message.Reason);
    }

    public Task TransitionToAsync(EnrollmentState newState, string? reason = null)
    {
        EnrollmentState oldState;
        lock (_stateLock)
        {
            if (_currentState == newState) return Task.CompletedTask;

            if (!IsValidTransition(_currentState, newState))
            {
                _logger.LogWarning("Invalid enrollment state transition rejected: {CurrentState} -> {NewState} ({Reason})",
                    _currentState, newState, reason ?? "N/A");
                return Task.CompletedTask;
            }

            oldState = _currentState;
            _currentState = newState;
        }

        _logger.LogInformation("Enrollment state changed: {OldState} -> {NewState} ({Reason})", oldState, newState, reason ?? "N/A");
        StateChanged?.Invoke(newState);
        return Task.CompletedTask;
    }

    private static bool IsValidTransition(EnrollmentState from, EnrollmentState to)
    {
        return (from, to) switch
        {
            (EnrollmentState.Unregistered, EnrollmentState.RegistrationPending) => true,
            (EnrollmentState.Unregistered, EnrollmentState.PendingApproval) => true,
            (EnrollmentState.Unregistered, EnrollmentState.Error) => true,

            (EnrollmentState.RegistrationPending, EnrollmentState.PendingApproval) => true,
            (EnrollmentState.RegistrationPending, EnrollmentState.Approved) => true,
            (EnrollmentState.RegistrationPending, EnrollmentState.Error) => true,
            (EnrollmentState.RegistrationPending, EnrollmentState.Unregistered) => true,

            (EnrollmentState.PendingApproval, EnrollmentState.Approved) => true,
            (EnrollmentState.PendingApproval, EnrollmentState.Rejected) => true,
            (EnrollmentState.PendingApproval, EnrollmentState.Revoked) => true,
            (EnrollmentState.PendingApproval, EnrollmentState.Error) => true,

            (EnrollmentState.Approved, EnrollmentState.Authenticated) => true,
            (EnrollmentState.Approved, EnrollmentState.Revoked) => true,
            (EnrollmentState.Approved, EnrollmentState.Rejected) => true,

            (EnrollmentState.Authenticated, EnrollmentState.Approved) => true, // Token expired or refresh needed
            (EnrollmentState.Authenticated, EnrollmentState.Revoked) => true,
            (EnrollmentState.Authenticated, EnrollmentState.Rejected) => true,

            (EnrollmentState.Error, EnrollmentState.RegistrationPending) => true,
            (EnrollmentState.Error, EnrollmentState.Unregistered) => true,

            // Terminal states
            (EnrollmentState.Rejected, EnrollmentState.RegistrationPending) => false,
            (EnrollmentState.Revoked, _) => false,

            _ => false
        };
    }
}
