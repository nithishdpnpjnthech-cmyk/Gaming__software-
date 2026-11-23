namespace GamingCafe.ClientAgent.Authentication;

public sealed class DeviceCredentials
{
    public Guid DeviceId { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTimeOffset TokenExpiresAtUtc { get; set; }
    public string? DeviceSecret { get; set; }
    public DateTimeOffset IssuedAtUtc { get; set; }

    public bool IsExpired(TimeSpan buffer) =>
        DateTimeOffset.UtcNow.Add(buffer) >= TokenExpiresAtUtc;

    public bool HasValidToken =>
        !string.IsNullOrEmpty(AccessToken) && !IsExpired(TimeSpan.FromSeconds(30));
}
