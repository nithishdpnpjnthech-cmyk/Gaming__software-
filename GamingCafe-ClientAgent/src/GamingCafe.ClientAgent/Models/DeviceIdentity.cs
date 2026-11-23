namespace GamingCafe.ClientAgent.Models;

/// <summary>
/// Persistent cryptographic identity for the Gaming PC.
/// </summary>
public record DeviceIdentity
{
    /// <summary>
    /// Unique cryptographically generated GUID for this Gaming PC.
    /// </summary>
    public required Guid DeviceId { get; init; }

    /// <summary>
    /// Machine hostname captured at initialization for operational identification.
    /// </summary>
    public required string Hostname { get; init; }

    /// <summary>
    /// Operating system platform information.
    /// </summary>
    public required string OperatingSystem { get; init; }

    /// <summary>
    /// Timestamp in UTC when this identity was generated.
    /// </summary>
    public required DateTime CreatedAtUtc { get; init; }

    /// <summary>
    /// Optional local hardware/motherboard fingerprint hash used purely as verification metadata (not authentication).
    /// </summary>
    public string? HardwareDigest { get; init; }
}
