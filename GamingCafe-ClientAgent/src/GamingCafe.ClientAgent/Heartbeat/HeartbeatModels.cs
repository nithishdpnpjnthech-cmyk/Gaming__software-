using System.Text.Json.Serialization;

namespace GamingCafe.ClientAgent.Heartbeat;

/// <summary>
/// Presence and health telemetry payload reported periodically to the Admin PC.
/// Does not contain customer activity, browsing logs, keystrokes, or screen captures.
/// </summary>
public record HeartbeatPayload
{
    [JsonPropertyName("deviceId")]
    public required Guid DeviceId { get; init; }

    [JsonPropertyName("hostname")]
    public required string Hostname { get; init; }

    [JsonPropertyName("agentVersion")]
    public required string AgentVersion { get; init; }

    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; init; } = "1.0";

    [JsonPropertyName("timestampUtc")]
    public required DateTime TimestampUtc { get; init; }

    [JsonPropertyName("agentStatus")]
    public required string AgentStatus { get; init; }

    [JsonPropertyName("serviceUptimeSeconds")]
    public required double ServiceUptimeSeconds { get; init; }

    [JsonPropertyName("connectionTransport")]
    public required string ConnectionTransport { get; init; } // "SignalR" or "REST"

    [JsonPropertyName("processMemoryBytes")]
    public long ProcessMemoryBytes { get; init; }

    [JsonPropertyName("lastCommunicationUtc")]
    public DateTime? LastCommunicationUtc { get; init; }
}

/// <summary>
/// Acknowledgement response from the backend acknowledging a heartbeat.
/// </summary>
public record HeartbeatAck
{
    [JsonPropertyName("acknowledged")]
    public bool Acknowledged { get; init; }

    [JsonPropertyName("serverTimestampUtc")]
    public DateTime ServerTimestampUtc { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "OK";
}
