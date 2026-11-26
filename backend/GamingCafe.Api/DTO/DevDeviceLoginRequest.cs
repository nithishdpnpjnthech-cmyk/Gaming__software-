using System.ComponentModel.DataAnnotations;

namespace GamingCafe.Api.DTOs;

public sealed class DevDeviceLoginRequest
{
    public Guid DeviceId { get; set; }

    [Required]
    [StringLength(128, MinimumLength = 43)]
    public string EnrollmentSecret { get; set; }
        = string.Empty;
}