using System;
using System.Collections.Generic;

namespace GamingCafe.Api.Models;

public partial class User
{
    public Guid Id { get; set; }

    public string Username { get; set; } = null!;

    public string? Email { get; set; }

    public string PasswordHash { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<DeviceCommand> DeviceCommands { get; set; } = new List<DeviceCommand>();

    public virtual ICollection<Device> Devices { get; set; } = new List<Device>();

    public virtual ICollection<SystemSetting> SystemSettings { get; set; } = new List<SystemSetting>();

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
