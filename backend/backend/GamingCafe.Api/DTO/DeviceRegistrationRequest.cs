using System.ComponentModel.DataAnnotations;
namespace GamingCafe.Api.DTOs;
public class DeviceRegistrationRequest
{
    public Guid? LocalDeviceId { get; set; }
    [StringLength(50)] public string? DeviceCode { get; set; }
    [Required, StringLength(100)] public string DisplayName { get; set; } = string.Empty;
    [StringLength(255)] public string? Hostname { get; set; }
    [StringLength(128)] public string? HardwareFingerprintHash { get; set; }
    [StringLength(30)] public string? AgentVersion { get; set; }
}
