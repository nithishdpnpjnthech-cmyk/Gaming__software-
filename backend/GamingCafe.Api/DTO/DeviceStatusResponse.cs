namespace GamingCafe.Api.DTOs;

public sealed class DeviceStatusResponse
{
    public Guid DeviceId { get; set; }

    public string DeviceCode { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Hostname { get; set; }

    public string? AgentVersion { get; set; }

    public string RegistrationStatus { get; set; } = string.Empty;

    public string ConnectivityStatus { get; set; } = string.Empty;

    public bool IsOnline { get; set; }

    public DateTime? LastSeenAt { get; set; }

    public string? LastIpAddress { get; set; }

    public string? ReportedPowerState { get; set; }

    public DateTime? LastStatusChangeAt { get; set; }

    public double? SecondsSinceLastHeartbeat { get; set; }

    public int HeartbeatIntervalSeconds { get; set; }

    public int OfflineTimeoutSeconds { get; set; }

    public DateTime ServerTimestampUtc { get; set; }
}