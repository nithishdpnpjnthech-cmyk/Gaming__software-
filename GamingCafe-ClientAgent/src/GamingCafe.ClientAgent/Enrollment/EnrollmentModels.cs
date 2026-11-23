using System.Text.Json.Serialization;

namespace GamingCafe.ClientAgent.Enrollment;

/// <summary>
/// DTO sent to backend to initiate device registration.
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
/// DTO returned from backend upon registration request.
/// </summary>
public record DeviceRegistrationResponse
{
    [JsonPropertyName("deviceId")]
    public Guid DeviceId { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "PendingApproval";

    [JsonPropertyName("deviceSecret")]
    public string? DeviceSecret { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("retryAfterSeconds")]
    public int? RetryAfterSeconds { get; init; }
}

/// <summary>
/// DTO returned when polling enrollment approval status.
/// </summary>
public record DeviceEnrollmentStatusResponse
{
    [JsonPropertyName("deviceId")]
    public Guid DeviceId { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "PendingApproval";

    [JsonPropertyName("deviceSecret")]
    public string? DeviceSecret { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("retryAfterSeconds")]
    public int? RetryAfterSeconds { get; init; }
}
