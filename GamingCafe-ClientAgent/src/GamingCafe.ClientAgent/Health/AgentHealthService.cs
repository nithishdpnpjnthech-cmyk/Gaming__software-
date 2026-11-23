using System.Diagnostics;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Enrollment;
using GamingCafe.ClientAgent.Realtime;
using Microsoft.Extensions.Options;

namespace GamingCafe.ClientAgent.Health;

/// <summary>
/// Maintains in-memory operational telemetry without exposing any public diagnostic ports.
/// </summary>
public class AgentHealthService : IAgentHealthService
{
    private readonly AgentConfiguration _options;
    private readonly DateTime _startTimeUtc;
    private Guid? _deviceId;
    private EnrollmentState _enrollmentState = EnrollmentState.Unregistered;
    private RealtimeConnectionState _signalRState = RealtimeConnectionState.Disconnected;
    private DateTime? _lastApiSuccessUtc;
    private DateTime? _lastHeartbeatSuccessUtc;
    private int _consecutiveErrors;
    private string? _lastErrorMessage;
    private readonly object _stateLock = new();

    public AgentHealthService(IOptions<AgentConfiguration> options)
    {
        _options = options.Value;
        _startTimeUtc = DateTime.UtcNow;
    }

    public void RecordServiceStarted() { }

    public void RecordDeviceId(Guid deviceId)
    {
        lock (_stateLock) _deviceId = deviceId;
    }

    public void RecordEnrollmentState(EnrollmentState state)
    {
        lock (_stateLock) _enrollmentState = state;
    }

    public void RecordSignalRState(RealtimeConnectionState state)
    {
        lock (_stateLock) _signalRState = state;
    }

    public void RecordApiSuccess()
    {
        lock (_stateLock)
        {
            _lastApiSuccessUtc = DateTime.UtcNow;
            _consecutiveErrors = 0;
            _lastErrorMessage = null;
        }
    }

    public void RecordApiFailure(string errorMessage)
    {
        lock (_stateLock)
        {
            _consecutiveErrors++;
            _lastErrorMessage = errorMessage;
        }
    }

    public void RecordHeartbeatSuccess()
    {
        lock (_stateLock)
        {
            _lastHeartbeatSuccessUtc = DateTime.UtcNow;
            _lastApiSuccessUtc = DateTime.UtcNow;
            _consecutiveErrors = 0;
            _lastErrorMessage = null;
        }
    }

    public AgentHealthReport GetCurrentHealth()
    {
        lock (_stateLock)
        {
            long memoryBytes = 0;
            try
            {
                using var proc = Process.GetCurrentProcess();
                memoryBytes = proc.WorkingSet64;
            }
            catch { }

            return new AgentHealthReport
            {
                AgentRunningState = "Running",
                ServiceUptimeSeconds = Math.Round((DateTime.UtcNow - _startTimeUtc).TotalSeconds, 2),
                DeviceId = _deviceId,
                EnrollmentState = _enrollmentState,
                SignalRConnectionState = _signalRState,
                LastSuccessfulApiRequestUtc = _lastApiSuccessUtc,
                LastSuccessfulHeartbeatUtc = _lastHeartbeatSuccessUtc,
                AgentVersion = _options.AgentVersion,
                ConsecutiveErrors = _consecutiveErrors,
                LastErrorMessage = _lastErrorMessage,
                ProcessMemoryBytes = memoryBytes
            };
        }
    }
}
