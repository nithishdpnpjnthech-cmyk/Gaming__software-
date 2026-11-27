namespace GamingCafe.Api.DTOs;

public sealed class DeviceStatusSummaryResponse
{
    public int TotalDevices { get; set; }

    public int ApprovedDevices { get; set; }

    public int OnlineDevices { get; set; }

    public int OfflineDevices { get; set; }

    public int NeverConnectedDevices { get; set; }

    public int PendingDevices { get; set; }

    public int RejectedDevices { get; set; }

    public int RevokedDevices { get; set; }

    public int HeartbeatIntervalSeconds { get; set; }

    public int OfflineTimeoutSeconds { get; set; }

    public DateTime ServerTimestampUtc { get; set; }
}