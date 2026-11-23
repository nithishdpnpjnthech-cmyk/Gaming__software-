using System;
using System.Collections.Generic;

namespace GamingCafe.Api.Models;

public partial class SecurityEvent
{
    public long Id { get; set; }

    public Guid? DeviceId { get; set; }

    public string EventType { get; set; } = null!;

    public string Outcome { get; set; } = null!;

    public string? Details { get; set; }

    public DateTime OccurredAt { get; set; }

    public virtual Device? Device { get; set; }
}
