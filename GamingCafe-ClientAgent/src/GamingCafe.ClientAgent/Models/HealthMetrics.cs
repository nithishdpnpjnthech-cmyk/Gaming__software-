namespace GamingCafe.ClientAgent.Models;

/// <summary>
/// Basic, privacy-respecting health metrics for the Client Agent.
/// Contains no personal user activity, keystrokes, screenshots, or browsing history.
/// </summary>
public record HealthMetrics
{
    public required string AgentServiceStatus { get; init; }
    public required double UptimeSeconds { get; init; }
    public DateTime? LastSuccessfulConnectionUtc { get; init; }
    public DateTime? LastSuccessfulHeartbeatUtc { get; init; }
    public required EnrollmentState CurrentEnrollmentState { get; init; }
    public required string NetworkConnectivityState { get; init; }
    public required string AgentVersion { get; init; }
    public long ProcessMemoryBytes { get; init; }
}
