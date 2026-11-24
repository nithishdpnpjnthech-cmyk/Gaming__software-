namespace GamingCafe.ClientAgent.Health;

public sealed class AgentHealthSnapshot
{
    public double CpuPercent { get; set; }
    public double MemoryUsageMb { get; set; }
    public int ThreadCount { get; set; }
    public double UptimeSeconds { get; set; }
    public long HeartbeatsSent { get; set; }
    public long HeartbeatsAcked { get; set; }
    public double LastLatencyMs { get; set; }
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}

public interface IAgentHealthService
{
    AgentHealthSnapshot GetSnapshot();
    void RecordHeartbeatSent();
    void RecordHeartbeatAcked(double latencyMs);
}
