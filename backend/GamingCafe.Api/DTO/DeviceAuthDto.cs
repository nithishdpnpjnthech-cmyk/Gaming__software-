using System.ComponentModel.DataAnnotations;

namespace GamingCafe.Api.DTOs;

public class DeviceAuthRequest
{
    [Required]
    [StringLength(50)]
    public string DeviceCode { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string HardwareFingerprintHash { get; set; } = string.Empty;
}

public class DeviceAuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public Guid DeviceId { get; set; }
    public string DeviceCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
