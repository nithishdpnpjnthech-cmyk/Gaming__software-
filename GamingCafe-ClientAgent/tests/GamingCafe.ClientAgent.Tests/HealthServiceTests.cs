using GamingCafe.ClientAgent.Health;
using Xunit;

namespace GamingCafe.ClientAgent.Tests;

public class HealthServiceTests
{
    [Fact]
    public void GetSnapshot_ReturnsReasonableMetrics()
    {
        var service = new AgentHealthService();
        var snapshot = service.GetSnapshot();

        Assert.NotNull(snapshot);
        Assert.True(snapshot.UptimeSeconds >= 0);
        Assert.True(snapshot.MemoryUsageMb > 0);
        Assert.True(snapshot.ThreadCount > 0);
    }

    [Fact]
    public void RecordHeartbeat_TracksSentAndAckedCounts()
    {
        var service = new AgentHealthService();

        service.RecordHeartbeatSent();
        service.RecordHeartbeatSent();
        service.RecordHeartbeatAcked(15.5);

        var snapshot = service.GetSnapshot();

        Assert.Equal(2, snapshot.HeartbeatsSent);
        Assert.Equal(1, snapshot.HeartbeatsAcked);
        Assert.Equal(15.5, snapshot.LastLatencyMs);
    }
}
