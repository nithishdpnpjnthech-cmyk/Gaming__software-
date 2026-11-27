using System;
using System.Collections.Generic;

namespace GamingCafe.Api.Models;

public partial class Device
{
    public Guid Id { get; set; }

    public string DeviceCode { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string? Hostname { get; set; }

    public string? HardwareFingerprintHash { get; set; }

    public string? AgentVersion { get; set; }

    public string RegistrationStatus { get; set; } = null!;

    public Guid? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<AgentLog> AgentLogs { get; set; } = new List<AgentLog>();

    public virtual User? ApprovedByNavigation { get; set; }

    public virtual ICollection<DeviceCommand> DeviceCommands { get; set; } = new List<DeviceCommand>();

    public virtual ICollection<DeviceConnectionLog> DeviceConnectionLogs { get; set; } = new List<DeviceConnectionLog>();

    public virtual ICollection<DeviceCredential> DeviceCredentials { get; set; } = new List<DeviceCredential>();

    public virtual ICollection<DeviceHeartbeat> DeviceHeartbeats { get; set; } = new List<DeviceHeartbeat>();

    public virtual DeviceNetworkInterface? DeviceNetworkInterface { get; set; }

    public virtual DeviceStatus? DeviceStatus { get; set; }

    public virtual ICollection<SecurityEvent> SecurityEvents { get; set; } = new List<SecurityEvent>();
}
