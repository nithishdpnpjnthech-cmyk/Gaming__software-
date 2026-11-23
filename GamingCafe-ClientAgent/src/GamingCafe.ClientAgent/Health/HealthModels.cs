using GamingCafe.ClientAgent.Enrollment;
using GamingCafe.ClientAgent.Realtime;

namespace GamingCafe.ClientAgent.Health;

/// <summary>
/// Operational health telemetry for the Client Agent.
/// Privacy-preserving: does not collect personal user data, keystrokes, or screen contents.
/// </summary>
public record AgentHealthReport
{
    public required string AgentRunningState { get; init; }
    public required double ServiceUptimeSeconds { get; init; }
    public Guid? DeviceId { get; init; }
    public EnrollmentState EnrollmentState { get; init; }
    public RealtimeConnectionState SignalRConnectionState { get; init; }
    public DateTime? LastSuccessfulApiRequestUtc { get; init; }
    public DateTime? LastSuccessfulHeartbeatUtc { get; init; }
    public required string AgentVersion { get; init; }
    public int ConsecutiveErrors { get; init; }
    public string? LastErrorMessage { get; init; }
    public long ProcessMemoryBytes { get; init; }
}

/// <summary>
/// Service contract for internal health tracking.
/// </summary>
public interface IAgentHealthService
{
    void RecordServiceStarted();
    void RecordDeviceId(Guid deviceId);
    void RecordEnrollmentState(EnrollmentState state);
    void RecordSignalRState(RealtimeConnectionState state);
    void RecordApiSuccess();
    void RecordApiFailure(string errorMessage);
    void RecordHeartbeatSuccess();
    AgentHealthReport GetCurrentHealth();
}
