using System.Text.Json.Serialization;

namespace GamingCafe.ClientAgent.Models.Dto;

/// <summary>
/// Proposal DTO sent by Client Agent when registering with the Admin backend.
/// </summary>
public record DeviceRegistrationRequest
{
    [JsonPropertyName("deviceId")]
    public required Guid DeviceId { get; init; }

    [JsonPropertyName("hostname")]
    public required string Hostname { get; init; }

    [JsonPropertyName("operatingSystem")]
    public required string OperatingSystem { get; init; }

    [JsonPropertyName("agentVersion")]
    public required string AgentVersion { get; init; }

    [JsonPropertyName("clientTimestampUtc")]
    public required DateTime ClientTimestampUtc { get; init; }

    [JsonPropertyName("hardwareDigest")]
    public string? HardwareDigest { get; init; }
}

/// <summary>
/// Proposal DTO returned by Admin backend in response to registration.
/// </summary>
public record DeviceRegistrationResponse
{
    [JsonPropertyName("deviceId")]
    public Guid DeviceId { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "Pending";

    [JsonPropertyName("deviceSecret")]
    public string? DeviceSecret { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("retryAfterSeconds")]
    public int? RetryAfterSeconds { get; init; }
}

/// <summary>
/// Proposal DTO returned when polling device enrollment status.
/// </summary>
public record DeviceEnrollmentStatusResponse
{
    [JsonPropertyName("deviceId")]
    public Guid DeviceId { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "Pending";

    [JsonPropertyName("deviceSecret")]
    public string? DeviceSecret { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("retryAfterSeconds")]
    public int? RetryAfterSeconds { get; init; }
}
