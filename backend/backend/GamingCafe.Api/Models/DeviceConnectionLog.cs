using System;
using System.Collections.Generic;

namespace GamingCafe.Api.Models;

public partial class DeviceConnectionLog
{
    public long Id { get; set; }

    public Guid DeviceId { get; set; }

    public string EventType { get; set; } = null!;

    public DateTime EventTime { get; set; }

    public string? Details { get; set; }

    public virtual Device Device { get; set; } = null!;
}
