namespace GamingCafe.Api.DTOs;
public sealed class HeartbeatMessage
{
    public string MessageId { get; set; } = "";
    public string Version { get; set; } = "";
    public string MessageType { get; set; } = "";
    public DateTimeOffset TimestampUtc { get; set; }
    public Guid DeviceId { get; set; }
    public long SequenceNumber { get; set; }
    public string AgentVersion { get; set; } = "";
    public double UpTimeSeconds { get; set; }
    public double CpuPercent { get; set; }
    public double MemoryUsageMb { get; set; }
}
