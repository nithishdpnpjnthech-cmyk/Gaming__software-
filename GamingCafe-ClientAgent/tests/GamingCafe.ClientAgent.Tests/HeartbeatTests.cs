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
    public async Task SendHeartbeat_WhenConnectedAndAcknowledged_ReturnsTrueAndIncrementsSequenceNumber()
    {
        var config = new AgentConfiguration();
        config.Identity.StorageDirectory = _tempDirectory;
        config.Heartbeat.TimeoutSeconds = 5;
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

        mockConn.OnSendAsync = (method, arg) =>
        {
            if (arg is HeartbeatMessage hm)
            {
                heartbeatService.HandleAcknowledgment(new HeartbeatAcknowledgment
                {
                    SequenceNumber = hm.SequenceNumber,
                    OriginalMessageId = hm.MessageId,
                    ServerTimestampUtc = DateTimeOffset.UtcNow
                });
            }
            return Task.CompletedTask;
        };

        var success = await heartbeatService.SendHeartbeatAsync();

        Assert.True(success);
        Assert.Equal(1, heartbeatService.SequenceNumber);
        Assert.NotNull(mockConn.LastSentMessage);
        Assert.IsType<HeartbeatMessage>(mockConn.LastSentMessage);

        var sentMsg = (HeartbeatMessage)mockConn.LastSentMessage;
        Assert.Equal(1, sentMsg.SequenceNumber);
        Assert.Equal(idService.CurrentIdentity.DeviceId, sentMsg.DeviceId);
        Assert.Equal(AgentConnectionStatus.Connected, sentMsg.ConnectionStatus);
    }

    [Fact]
    public async Task SendHeartbeat_UsesBackendDeviceId_WhenAvailable()
    {
        var config = new AgentConfiguration();
        config.Identity.StorageDirectory = _tempDirectory;
        config.Heartbeat.TimeoutSeconds = 5;
        var configService = new MockConfigurationService(config);

        var idStore = new DeviceIdentityStore(configService, NullLogger<DeviceIdentityStore>.Instance);
        var idService = new DeviceIdentityService(idStore, configService, NullLogger<DeviceIdentityService>.Instance);
        
        var expectedBackendId = Guid.NewGuid();
        var identity = idService.GetOrCreateIdentity();
        identity.BackendDeviceId = expectedBackendId;
        idStore.SaveIdentity(identity);

        var healthService = new AgentHealthService();
        var mockConn = new MockSignalRConnectionManager { IsConnected = true };

        var heartbeatService = new HeartbeatService(
            mockConn,
            idService,
            healthService,
            configService,
            NullLogger<HeartbeatService>.Instance);

        mockConn.OnSendAsync = (method, arg) =>
        {
            if (arg is HeartbeatMessage hm)
            {
                heartbeatService.HandleAcknowledgment(new HeartbeatAcknowledgment
                {
                    SequenceNumber = hm.SequenceNumber,
                    OriginalMessageId = hm.MessageId,
                    ServerTimestampUtc = DateTimeOffset.UtcNow
                });
            }
            return Task.CompletedTask;
        };

        var success = await heartbeatService.SendHeartbeatAsync();

        Assert.True(success);
        var sentMsg = Assert.IsType<HeartbeatMessage>(mockConn.LastSentMessage);
        Assert.Equal(expectedBackendId, sentMsg.DeviceId);
    }

    [Fact]
    public async Task SendHeartbeat_WhenNoAcknowledgment_TimesOutAndIncrementsMissedCount()
    {
        var config = new AgentConfiguration();
        config.Identity.StorageDirectory = _tempDirectory;
        config.Heartbeat.TimeoutSeconds = 1; // 1 second timeout for test
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

        // Do not acknowledge the heartbeat
        var success = await heartbeatService.SendHeartbeatAsync();

        Assert.False(success);
        Assert.Equal(1, heartbeatService.SequenceNumber);
        Assert.Equal(1, heartbeatService.MissedHeartbeats);
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

    [Fact]
    public async Task SendHeartbeat_WhenConsecutiveMissedThresholdReached_TriggersRecovery()
    {
        var config = new AgentConfiguration();
        config.Identity.StorageDirectory = _tempDirectory;
        config.Heartbeat.TimeoutSeconds = 1;
        config.Heartbeat.MaxMissedHeartbeats = 2;
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

        // Send two unacknowledged heartbeats
        await heartbeatService.SendHeartbeatAsync();
        await heartbeatService.SendHeartbeatAsync();

        Assert.Equal(2, heartbeatService.MissedHeartbeats);
        // Wait briefly for the background recovery task
        await Task.Delay(200);
        Assert.True(mockConn.StopCalled);
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
        public string? ConnectionId { get; set; } = "mock-conn-123";
        public AgentConnectionStatus Status { get; set; } = AgentConnectionStatus.Connected;
        public event Action<AgentConnectionStatus, string?>? ConnectionStateChanged;
        public object? LastSentMessage { get; private set; }
        public bool StopCalled { get; private set; }
        public Func<string, object?, Task>? OnSendAsync { get; set; }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCalled = true;
            IsConnected = false;
            Status = AgentConnectionStatus.Disconnected;
            ConnectionStateChanged?.Invoke(AgentConnectionStatus.Disconnected, "StopAsync called");
            return Task.CompletedTask;
        }

        public async Task SendAsync(string methodName, object? arg1, CancellationToken cancellationToken = default)
        {
            LastSentMessage = arg1;
            if (OnSendAsync != null)
            {
                await OnSendAsync(methodName, arg1);
            }
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
