using System.Text.Json.Serialization;

namespace GamingCafe.ClientAgent.Models.Dto;

/// <summary>
/// Proposal DTO sent periodically by Client Agent to report presence and health.
/// Never contains passwords, private keys, or raw secrets.
/// </summary>
public record HeartbeatRequest
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

    [JsonPropertyName("uptimeSeconds")]
    public required double UptimeSeconds { get; init; }

    [JsonPropertyName("health")]
    public required HealthMetrics Health { get; init; }
}

/// <summary>
/// Proposal DTO returned by Admin backend acknowledging heartbeat.
/// </summary>
public record HeartbeatResponse
{
    [JsonPropertyName("acknowledged")]
    public bool Acknowledged { get; init; }

    [JsonPropertyName("serverTimestampUtc")]
    public DateTime ServerTimestampUtc { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "OK";

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
