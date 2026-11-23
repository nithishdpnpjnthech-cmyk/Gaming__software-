using System;
using System.Collections.Generic;

namespace GamingCafe.Api.Models;

public partial class DeviceCommandLog
{
    public long Id { get; set; }

    public Guid CommandId { get; set; }

    public string EventType { get; set; } = null!;

    public string? Details { get; set; }

    public DateTime EventTime { get; set; }

    public virtual DeviceCommand Command { get; set; } = null!;
}
