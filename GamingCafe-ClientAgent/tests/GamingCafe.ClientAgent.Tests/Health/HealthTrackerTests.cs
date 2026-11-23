using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Enrollment;
using GamingCafe.ClientAgent.Health;
using GamingCafe.ClientAgent.Realtime;
using Microsoft.Extensions.Options;
using Xunit;

namespace GamingCafe.ClientAgent.Tests.Health;

public class HealthTrackerTests
{
    [Fact]
    public void AgentHealthService_LifecycleAndMetrics_TracksOperationalState()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AgentConfiguration
        {
            AgentVersion = "1.0.0-test"
        });

        var healthService = new AgentHealthService(options);
        var deviceId = Guid.NewGuid();

        healthService.RecordServiceStarted();
        healthService.RecordDeviceId(deviceId);
        healthService.RecordEnrollmentState(EnrollmentState.Authenticated);
        healthService.RecordSignalRState(RealtimeConnectionState.Connected);
        healthService.RecordHeartbeatSuccess();

        var report = healthService.GetCurrentHealth();

        Assert.Equal("Running", report.AgentRunningState);
        Assert.Equal(deviceId, report.DeviceId);
        Assert.Equal(EnrollmentState.Authenticated, report.EnrollmentState);
        Assert.Equal(RealtimeConnectionState.Connected, report.SignalRConnectionState);
        Assert.NotNull(report.LastSuccessfulHeartbeatUtc);
        Assert.Equal(0, report.ConsecutiveErrors);
        Assert.Null(report.LastErrorMessage);
        Assert.True(report.ServiceUptimeSeconds >= 0);
    }
}
