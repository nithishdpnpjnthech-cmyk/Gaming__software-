using System.Diagnostics;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Interfaces;
using GamingCafe.ClientAgent.Models;
using Microsoft.Extensions.Options;

namespace GamingCafe.ClientAgent.Health;

/// <summary>
/// Maintains basic, privacy-preserving operational metrics for the Agent service.
/// </summary>
public class HealthTracker : IHealthTracker
{
    private readonly AgentOptions _options;
    private readonly DateTime _startTimeUtc;
    private DateTime? _lastSuccessfulConnectionUtc;
    private DateTime? _lastSuccessfulHeartbeatUtc;
    private EnrollmentState _currentState = EnrollmentState.Unregistered;
    private string _networkConnectivityState = "Unknown";
    private readonly object _lock = new();

    public HealthTracker(IOptions<AgentOptions> options)
    {
        _options = options.Value;
        _startTimeUtc = DateTime.UtcNow;
    }

    public void RecordServiceStarted()
    {
        lock (_lock)
        {
            _networkConnectivityState = "Initializing";
        }
    }

    public void RecordConnectionAttempt(bool success)
    {
        lock (_lock)
        {
            if (success)
            {
                _lastSuccessfulConnectionUtc = DateTime.UtcNow;
                _networkConnectivityState = "Connected";
            }
            else
            {
                _networkConnectivityState = "DisconnectedOrUnreachable";
            }
        }
    }

    public void RecordHeartbeatSent(bool success)
    {
        lock (_lock)
        {
            if (success)
            {
                _lastSuccessfulHeartbeatUtc = DateTime.UtcNow;
                _lastSuccessfulConnectionUtc = DateTime.UtcNow;
                _networkConnectivityState = "Connected";
            }
        }
    }

    public void RecordStateChange(EnrollmentState state)
    {
        lock (_lock)
        {
            _currentState = state;
        }
    }

    public HealthMetrics GetCurrentMetrics()
    {
        lock (_lock)
        {
            var uptime = (DateTime.UtcNow - _startTimeUtc).TotalSeconds;
            long memoryBytes = 0;
            try
            {
                using var proc = Process.GetCurrentProcess();
                memoryBytes = proc.WorkingSet64;
            }
            catch
            {
                // Best effort memory tracking
            }

            return new HealthMetrics
            {
                AgentServiceStatus = "Running",
                UptimeSeconds = Math.Round(uptime, 2),
                LastSuccessfulConnectionUtc = _lastSuccessfulConnectionUtc,
                LastSuccessfulHeartbeatUtc = _lastSuccessfulHeartbeatUtc,
                CurrentEnrollmentState = _currentState,
                NetworkConnectivityState = _networkConnectivityState,
                AgentVersion = _options.AgentVersion,
                ProcessMemoryBytes = memoryBytes
            };
        }
    }
}
