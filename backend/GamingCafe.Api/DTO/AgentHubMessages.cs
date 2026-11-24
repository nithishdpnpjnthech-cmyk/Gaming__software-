using System.Text.Json.Serialization;

namespace GamingCafe.Api.DTOs;

public class HeartbeatRequestMessage
{
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("timestampUtc")]
    public DateTimeOffset TimestampUtc { get; set; }

    [JsonPropertyName("deviceId")]
    public Guid DeviceId { get; set; }

    [JsonPropertyName("messageType")]
    public string MessageType { get; set; } = "Heartbeat";

    [JsonPropertyName("sequenceNumber")]
    public long SequenceNumber { get; set; }

    [JsonPropertyName("agentVersion")]
    public string AgentVersion { get; set; } = string.Empty;

    [JsonPropertyName("upTimeSeconds")]
    public double UpTimeSeconds { get; set; }

    [JsonPropertyName("cpuPercent")]
    public double CpuPercent { get; set; }

    [JsonPropertyName("memoryUsageMb")]
    public double MemoryUsageMb { get; set; }
}

public class HeartbeatAcknowledgmentMessage
{
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("timestampUtc")]
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("deviceId")]
    public Guid DeviceId { get; set; }

    [JsonPropertyName("messageType")]
    public string MessageType { get; set; } = "HeartbeatAck";

    [JsonPropertyName("sequenceNumber")]
    public long SequenceNumber { get; set; }

    [JsonPropertyName("originalMessageId")]
    public string OriginalMessageId { get; set; } = string.Empty;

    [JsonPropertyName("serverTimestampUtc")]
    public DateTimeOffset ServerTimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}
