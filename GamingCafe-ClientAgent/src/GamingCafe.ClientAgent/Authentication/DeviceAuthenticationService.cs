using GamingCafe.ClientAgent.Security;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Authentication;

public sealed class DeviceAuthenticationService : IDeviceAuthenticationService
{
    private readonly ISecureCredentialStore _credentialStore;
    private readonly ILogger<DeviceAuthenticationService> _logger;
    private DeviceCredentials? _currentCredentials;
    private readonly object _lock = new();

    public DeviceAuthenticationService(
        ISecureCredentialStore credentialStore,
        ILogger<DeviceAuthenticationService> logger)
    {
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _currentCredentials = _credentialStore.LoadCredentials();
    }

    public bool IsAuthenticated
    {
        get
        {
            lock (_lock)
            {
                return _currentCredentials != null && _currentCredentials.HasValidToken;
            }
        }
    }

    public DeviceCredentials? CurrentCredentials
    {
        get
        {
            lock (_lock)
            {
                return _currentCredentials;
            }
        }
    }

    public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_currentCredentials == null)
            {
                return Task.FromResult<string?>(null);
            }

            if (_currentCredentials.IsExpired(TimeSpan.FromSeconds(30)))
            {
                _logger.LogWarning("Current access token is expired or expiring soon.");
                return Task.FromResult<string?>(null);
            }

            return Task.FromResult<string?>(_currentCredentials.AccessToken);
        }
    }

    public void SetCredentials(DeviceCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        lock (_lock)
        {
            _credentialStore.SaveCredentials(credentials);
            _currentCredentials = credentials;
            _logger.LogInformation("Device credentials updated. Token valid until: {ExpiresUtc}", credentials.TokenExpiresAtUtc);
        }
    }

    public void InvalidateCredentials()
    {
        lock (_lock)
        {
            _logger.LogWarning("Invalidating device credentials.");
            _credentialStore.ClearCredentials();
            _currentCredentials = null;
        }
    }
}
