namespace GamingCafe.ClientAgent.Identity;

public sealed class DeviceIdentity
{
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string HardwareIdentifier { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string ClientVersion { get; set; } = string.Empty;
    public DateTimeOffset FirstEnrolledAtUtc { get; set; }
    public DateTimeOffset LastUpdatedAtUtc { get; set; }
}
