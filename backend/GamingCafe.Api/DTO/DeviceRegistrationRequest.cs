
namespace GamingCafe.Api.DTOs;

public class DeviceRegistrationRequest
{
    public string DeviceCode { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Hostname { get; set; }

    public string? HardwareFingerprintHash { get; set; }

    public string? AgentVersion { get; set; }
}
