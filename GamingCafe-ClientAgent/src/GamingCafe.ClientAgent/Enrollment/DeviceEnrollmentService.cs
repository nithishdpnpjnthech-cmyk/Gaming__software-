using GamingCafe.ClientAgent.Authentication;
using GamingCafe.ClientAgent.Identity;
using GamingCafe.ClientAgent.Messaging;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Enrollment;

public sealed class DeviceEnrollmentService : IDeviceEnrollmentService
{
    private readonly IDeviceIdentityService _identityService;
    private readonly IDeviceAuthenticationService _authService;
    private readonly ILogger<DeviceEnrollmentService> _logger;
    private EnrollmentState _currentState = EnrollmentState.Unregistered;
    private readonly object _stateLock = new();

    public event Action<EnrollmentState>? StateChanged;

    public DeviceEnrollmentService(
        IDeviceIdentityService identityService,
        IDeviceAuthenticationService authService,
        ILogger<DeviceEnrollmentService> logger)
    {
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
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
                _logger.LogInformation("Device {DeviceId} already enrolled and authenticated.", identity.DeviceId);
            }
            else if (creds != null)
            {
                _currentState = EnrollmentState.Approved;
                _logger.LogInformation("Device {DeviceId} approved, token refresh needed.", identity.DeviceId);
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

        if (message.IsApproved)
        {
            _logger.LogInformation("Device approval received. Zone assigned: {Zone}", message.AssignedZone ?? "Default");
            return TransitionToAsync(EnrollmentState.Approved, "Approved by administrator.");
        }
        else
        {
            _logger.LogWarning("Device enrollment rejected. Reason: {Reason}", message.RejectionReason ?? "None provided.");
            return TransitionToAsync(EnrollmentState.Rejected, message.RejectionReason);
        }
    }

    public Task HandleAuthenticationMessageAsync(DeviceAuthenticationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

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

        _logger.LogCritical("Device revocation received! Reason: {Reason}", message.Reason);
        _authService.InvalidateCredentials();
        return TransitionToAsync(EnrollmentState.Revoked, message.Reason);
    }

    public Task TransitionToAsync(EnrollmentState newState, string? reason = null)
    {
        EnrollmentState oldState;
        lock (_stateLock)
        {
            if (_currentState == newState) return Task.CompletedTask;

            oldState = _currentState;
            _currentState = newState;
        }

        _logger.LogInformation("Enrollment state changed: {OldState} -> {NewState} ({Reason})", oldState, newState, reason ?? "N/A");
        StateChanged?.Invoke(newState);
        return Task.CompletedTask;
    }
}
