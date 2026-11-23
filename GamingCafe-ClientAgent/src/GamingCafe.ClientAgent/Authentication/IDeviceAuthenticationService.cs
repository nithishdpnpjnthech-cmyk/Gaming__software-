namespace GamingCafe.ClientAgent.Authentication;

public interface IDeviceAuthenticationService
{
    bool IsAuthenticated { get; }
    DeviceCredentials? CurrentCredentials { get; }
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    void SetCredentials(DeviceCredentials credentials);
    void InvalidateCredentials();
}
