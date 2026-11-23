using GamingCafe.ClientAgent.Communication;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Health;
using GamingCafe.ClientAgent.Heartbeat;
using GamingCafe.ClientAgent.Identity;
using GamingCafe.ClientAgent.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GamingCafe.ClientAgent.Tests;

public class HeartbeatTests : IDisposable
{
    private readonly string _tempDirectory;

    public HeartbeatTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "AgentTest_Heartbeat_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task SendHeartbeat_WhenConnected_IncrementsSequenceNumber()
    {
        var config = new AgentConfiguration();
        config.Identity.StorageDirectory = _tempDirectory;
        var configService = new MockConfigurationService(config);

        var idStore = new DeviceIdentityStore(configService, NullLogger<DeviceIdentityStore>.Instance);
        var idService = new DeviceIdentityService(idStore, configService, NullLogger<DeviceIdentityService>.Instance);
        var healthService = new AgentHealthService();
        var mockConn = new MockSignalRConnectionManager { IsConnected = true };

        var heartbeatService = new HeartbeatService(
            mockConn,
            idService,
            healthService,
            configService,
            NullLogger<HeartbeatService>.Instance);

        var success = await heartbeatService.SendHeartbeatAsync();

        Assert.True(success);
        Assert.Equal(1, heartbeatService.SequenceNumber);
        Assert.NotNull(mockConn.LastSentMessage);
        Assert.IsType<HeartbeatMessage>(mockConn.LastSentMessage);

        var sentMsg = (HeartbeatMessage)mockConn.LastSentMessage;
        Assert.Equal(1, sentMsg.SequenceNumber);
        Assert.Equal(idService.CurrentIdentity.DeviceId, sentMsg.DeviceId);
    }

    [Fact]
    public void HandleAcknowledgment_CalculatesLatencyAndResetsMissedCount()
    {
        var config = new AgentConfiguration();
        var configService = new MockConfigurationService(config);
        var mockConn = new MockSignalRConnectionManager { IsConnected = true };
        var healthService = new AgentHealthService();

        var heartbeatService = new HeartbeatService(
            mockConn,
            new MockDeviceIdentityService(),
            healthService,
            configService,
            NullLogger<HeartbeatService>.Instance);

        var ack = new HeartbeatAcknowledgment
        {
            SequenceNumber = 1,
            OriginalMessageId = Guid.NewGuid().ToString("N"),
            ServerTimestampUtc = DateTimeOffset.UtcNow
        };

        heartbeatService.HandleAcknowledgment(ack);

        Assert.Equal(0, heartbeatService.MissedHeartbeats);
        Assert.True(heartbeatService.LastLatencyMs >= 0.0);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch { /* ignore */ }
    }

    private sealed class MockSignalRConnectionManager : ISignalRConnectionManager
    {
        public bool IsConnected { get; set; }
        public object? LastSentMessage { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendAsync(string methodName, object? arg1, CancellationToken cancellationToken = default)
        {
            LastSentMessage = arg1;
            return Task.CompletedTask;
        }
        public Task<T> InvokeAsync<T>(string methodName, object? arg1, CancellationToken cancellationToken = default) =>
            Task.FromResult(default(T)!);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class MockDeviceIdentityService : IDeviceIdentityService
    {
        public DeviceIdentity CurrentIdentity { get; } = new()
        {
            DeviceId = Guid.NewGuid(),
            DeviceName = "TEST-PC-01",
            ClientVersion = "1.0.0"
        };
        public DeviceIdentity GetOrCreateIdentity() => CurrentIdentity;
        public void RefreshIdentity() { }
    }
}
