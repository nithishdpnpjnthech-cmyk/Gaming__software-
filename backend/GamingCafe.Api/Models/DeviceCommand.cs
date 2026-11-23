using System;
using System.Collections.Generic;

namespace GamingCafe.Api.Models;

public partial class DeviceCommand
{
    public Guid Id { get; set; }

    public Guid DeviceId { get; set; }

    public Guid RequestedBy { get; set; }

    public string CommandType { get; set; } = null!;

    public string CommandStatus { get; set; } = null!;

    public string? Reason { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? DispatchedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? ErrorMessage { get; set; }

    public virtual Device Device { get; set; } = null!;

    public virtual ICollection<DeviceCommandLog> DeviceCommandLogs { get; set; } = new List<DeviceCommandLog>();

    public virtual User RequestedByNavigation { get; set; } = null!;
}
