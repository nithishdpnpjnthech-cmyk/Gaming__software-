using System;
using System.Collections.Generic;
using System.Net;

namespace GamingCafe.Api.Models;

public partial class DeviceStatus
{
    public Guid DeviceId { get; set; }

    public DateTime? LastSeenAt { get; set; }

    public IPAddress? LastIpAddress { get; set; }

    public string? ReportedPowerState { get; set; }

    public DateTime? LastStatusChangeAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Device Device { get; set; } = null!;
}
