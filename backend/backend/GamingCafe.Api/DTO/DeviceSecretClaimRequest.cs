using System.ComponentModel.DataAnnotations;

namespace GamingCafe.Api.DTOs;

public class DeviceSecretClaimRequest
{
    [Required]
    public Guid LocalDeviceId { get; set; }
}
