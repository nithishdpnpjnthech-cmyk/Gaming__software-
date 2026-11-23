using System;
using System.Collections.Generic;
using System.Net;

namespace GamingCafe.Api.Models;

public partial class DeviceHeartbeat
{
    public long Id { get; set; }

    public Guid DeviceId { get; set; }

    public DateTime ReceivedAt { get; set; }

    public string? AgentVersion { get; set; }

    public IPAddress? SourceIp { get; set; }

    public virtual Device Device { get; set; } = null!;
}
