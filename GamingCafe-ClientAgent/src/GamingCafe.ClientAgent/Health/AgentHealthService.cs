using System.Diagnostics;

namespace GamingCafe.ClientAgent.Health;

public sealed class AgentHealthService : IAgentHealthService
{
    private readonly Process _currentProcess = Process.GetCurrentProcess();
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private long _heartbeatsSent;
    private long _heartbeatsAcked;
    private double _lastLatencyMs;
    private TimeSpan _lastTotalProcessorTime;
    private DateTimeOffset _lastCpuCheckTime = DateTimeOffset.UtcNow;
    private readonly object _lock = new();

    public AgentHealthService()
    {
        _lastTotalProcessorTime = _currentProcess.TotalProcessorTime;
    }

    public AgentHealthSnapshot GetSnapshot()
    {
        _currentProcess.Refresh();

        var now = DateTimeOffset.UtcNow;
        double cpuPercent = 0.0;

        lock (_lock)
        {
            var cpuTimeDelta = (_currentProcess.TotalProcessorTime - _lastTotalProcessorTime).TotalMilliseconds;
            var totalTimeDelta = (now - _lastCpuCheckTime).TotalMilliseconds * Environment.ProcessorCount;

            if (totalTimeDelta > 100)
            {
                cpuPercent = Math.Clamp((cpuTimeDelta / totalTimeDelta) * 100.0, 0.0, 100.0);
                _lastTotalProcessorTime = _currentProcess.TotalProcessorTime;
                _lastCpuCheckTime = now;
            }
        }

        var memoryMb = _currentProcess.WorkingSet64 / (1024.0 * 1024.0);

        return new AgentHealthSnapshot
        {
            CpuPercent = Math.Round(cpuPercent, 1),
            MemoryUsageMb = Math.Round(memoryMb, 1),
            ThreadCount = _currentProcess.Threads.Count,
            UptimeSeconds = Math.Round((now - _startedAt).TotalSeconds, 1),
            HeartbeatsSent = Interlocked.Read(ref _heartbeatsSent),
            HeartbeatsAcked = Interlocked.Read(ref _heartbeatsAcked),
            LastLatencyMs = _lastLatencyMs,
            TimestampUtc = now
        };
    }

    public void RecordHeartbeatSent()
    {
        Interlocked.Increment(ref _heartbeatsSent);
    }

    public void RecordHeartbeatAcked(double latencyMs)
    {
        Interlocked.Increment(ref _heartbeatsAcked);
        lock (_lock)
        {
            _lastLatencyMs = latencyMs;
        }
    }
}
