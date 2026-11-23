using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Authentication;

/// <summary>
/// Service contract for device authentication and credential management.
/// </summary>
public interface IDeviceAuthenticationService
{
    Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default);
    Task<string?> GetDeviceSecretAsync(CancellationToken cancellationToken = default);
    Task StoreIssuedSecretAsync(string deviceSecret, CancellationToken cancellationToken = default);
    Task RevokeCredentialsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Manages device authentication state, token acquisition, and revocation purging.
/// </summary>
public class DeviceAuthenticationService : IDeviceAuthenticationService
{
    public const string DeviceSecretKey = "agent_auth_secret";

    private readonly IDeviceCredentialStore _credentialStore;
    private readonly ILogger<DeviceAuthenticationService> _logger;

    public DeviceAuthenticationService(
        IDeviceCredentialStore credentialStore,
        ILogger<DeviceAuthenticationService> logger)
    {
        _credentialStore = credentialStore;
        _logger = logger;
    }

    public async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        var secret = await GetDeviceSecretAsync(cancellationToken);
        return !string.IsNullOrWhiteSpace(secret);
    }

    public async Task<string?> GetDeviceSecretAsync(CancellationToken cancellationToken = default)
    {
        return await _credentialStore.GetCredentialAsync(DeviceSecretKey, cancellationToken);
    }

    public async Task StoreIssuedSecretAsync(string deviceSecret, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceSecret);
        await _credentialStore.SaveCredentialAsync(DeviceSecretKey, deviceSecret, cancellationToken);
        _logger.LogInformation("New device authentication credentials successfully stored.");
    }

    public async Task RevokeCredentialsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Revoking device credentials. Clearing local encrypted token.");
        await _credentialStore.DeleteCredentialAsync(DeviceSecretKey, cancellationToken);
    }
}
