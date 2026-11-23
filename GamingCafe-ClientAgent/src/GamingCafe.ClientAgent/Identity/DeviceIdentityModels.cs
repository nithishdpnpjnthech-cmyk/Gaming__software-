namespace GamingCafe.ClientAgent.Identity;

/// <summary>
/// Persistent cryptographic identity for this Gaming PC.
/// </summary>
public record DeviceIdentity
{
    /// <summary>
    /// Unique cryptographically generated GUID for this Gaming PC.
    /// </summary>
    public required Guid DeviceId { get; init; }

    /// <summary>
    /// Hostname of the gaming PC captured for human-readable operator identification.
    /// </summary>
    public required string Hostname { get; init; }

    /// <summary>
    /// Operating system platform information.
    /// </summary>
    public required string OperatingSystem { get; init; }

    /// <summary>
    /// UTC timestamp of when this device identity was first created.
    /// </summary>
    public required DateTime CreatedAtUtc { get; init; }

    /// <summary>
    /// Hardware digest fingerprint for clone-detection metadata (never used as secret).
    /// </summary>
    public string? HardwareDigest { get; init; }
}

/// <summary>
/// Service contract for device identity management.
/// </summary>
public interface IDeviceIdentityService
{
    Task<DeviceIdentity> GetOrCreateIdentityAsync(CancellationToken cancellationToken = default);
    bool HasIdentity();
}

/// <summary>
/// Storage abstraction contract for device identity persistence.
/// </summary>
public interface IDeviceIdentityStore
{
    Task<DeviceIdentity?> LoadIdentityAsync(CancellationToken cancellationToken = default);
    Task SaveIdentityAsync(DeviceIdentity identity, CancellationToken cancellationToken = default);
    bool Exists();
    void RecoverCorruptStorage();
}
