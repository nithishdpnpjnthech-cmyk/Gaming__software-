namespace GamingCafe.Api.DTOs;
public sealed class HeartbeatAcknowledgment
{
    public string MessageId { get; init; } = Guid.NewGuid().ToString("N");
    public string Version { get; init; } = "1.0";
    public string MessageType { get; init; } = "HeartbeatAck";
    public Guid DeviceId { get; init; }
    public DateTimeOffset TimestampUtc { get; init; }
    public long SequenceNumber { get; init; }
    public string OriginalMessageId { get; init; } = "";
    public DateTimeOffset ServerTimestampUtc { get; init; }
}
