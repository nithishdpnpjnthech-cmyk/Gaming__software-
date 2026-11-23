using System.Text.Json.Serialization;

namespace GamingCafe.ClientAgent.Realtime;

/// <summary>
/// Event received when backend acknowledges a real-time status update or presence beacon.
/// </summary>
public record DeviceStatusAcknowledgedEvent
{
    [JsonPropertyName("deviceId")]
    public Guid DeviceId { get; init; }

    [JsonPropertyName("serverTimestampUtc")]
    public DateTime ServerTimestampUtc { get; init; }

    [JsonPropertyName("acknowledged")]
    public bool Acknowledged { get; init; }
}

/// <summary>
/// Event received when cafe administrator changes device enrollment (e.g. Approved or Revoked).
/// </summary>
public record EnrollmentStatusChangedEvent
{
    [JsonPropertyName("deviceId")]
    public Guid DeviceId { get; init; }

    [JsonPropertyName("newStatus")]
    public required string NewStatus { get; init; } // e.g. "Approved", "Revoked", "Rejected"

    [JsonPropertyName("deviceSecret")]
    public string? DeviceSecret { get; init; }

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

/// <summary>
/// Broadcast event from Admin server reporting server health or restart readiness.
/// </summary>
public record ServerHealthNotificationEvent
{
    [JsonPropertyName("serverStatus")]
    public required string ServerStatus { get; init; }

    [JsonPropertyName("timestampUtc")]
    public DateTime TimestampUtc { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

/// <summary>
/// Notification for planned maintenance window.
/// </summary>
public record MaintenanceNotificationEvent
{
    [JsonPropertyName("scheduledTimeUtc")]
    public DateTime ScheduledTimeUtc { get; init; }

    [JsonPropertyName("durationMinutes")]
    public int DurationMinutes { get; init; }

    [JsonPropertyName("noticeMessage")]
    public required string NoticeMessage { get; init; }
}
