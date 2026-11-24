using System;
using System.Collections.Generic;

namespace GamingCafe.Api.Models;

public partial class AgentLog
{
    public long Id { get; set; }

    public Guid EventId { get; set; }

    public Guid DeviceId { get; set; }

    public string LogLevel { get; set; } = null!;

    public string EventType { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string? ExceptionDetails { get; set; }

    public Guid? CorrelationId { get; set; }

    public DateTime OccurredAt { get; set; }

    public DateTime ReceivedAt { get; set; }

    public virtual Device Device { get; set; } = null!;
}
