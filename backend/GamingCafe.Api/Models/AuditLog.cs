using System;
using System.Collections.Generic;

namespace GamingCafe.Api.Models;

public partial class AuditLog
{
    public long Id { get; set; }

    public Guid? UserId { get; set; }

    public string Action { get; set; } = null!;

    public string? TargetType { get; set; }

    public Guid? TargetId { get; set; }

    public string? Details { get; set; }

    public DateTime OccurredAt { get; set; }

    public virtual User? User { get; set; }
}
