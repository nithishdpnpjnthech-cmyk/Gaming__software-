using System.Net;
using GamingCafe.ClientAgent.Authentication;
using GamingCafe.ClientAgent.Communication.ApiModels;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Health;
using GamingCafe.ClientAgent.Heartbeat;
using GamingCafe.ClientAgent.Identity;
using GamingCafe.ClientAgent.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GamingCafe.ClientAgent.Tests.Heartbeat;

public class HeartbeatServiceTests
{
    private readonly MockSignalRHubClient _mockSignalR = new();
    private readonly MockBackendApiClient _mockApi = new();
    private readonly TestCredentialStore _credentialStore = new();
    private readonly DeviceAuthenticationService _authService;
    private readonly AgentHealthService _healthService;
    private readonly IOptions<AgentConfiguration> _options;
    private readonly DeviceIdentity _testIdentity;

    public HeartbeatServiceTests()
    {
        _authService = new DeviceAuthenticationService(_credentialStore, NullLogger<DeviceAuthenticationService>.Instance);
        var config = new AgentConfiguration
        {
            BackendApiBaseUrl = "https://127.0.0.1:5001",
            AgentVersion = "1.0.0-test"
        };
        _options = Microsoft.Extensions.Options.Options.Create(config);
        _healthService = new AgentHealthService(_options);

        _testIdentity = new DeviceIdentity
        {
            DeviceId = Guid.NewGuid(),
            Hostname = "HB-TEST-PC",
            OperatingSystem = "Windows 11",
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private HeartbeatService CreateService() =>
        new(_mockSignalR, _mockApi, _authService, _healthService, _options, NullLogger<HeartbeatService>.Instance);

    [Fact]
    public async Task SendHeartbeatAsync_WithoutSecret_AbortsSafelyAndReturnsFalse()
    {
        var service = CreateService();
        var result = await service.SendHeartbeatAsync(_testIdentity);

        Assert.False(result);
        Assert.Empty(_mockSignalR.SentHeartbeats);
        Assert.Empty(_mockApi.CapturedHeartbeats);
    }

    [Fact]
    public async Task SendHeartbeatAsync_SignalRConnected_UsesSignalRTransport()
    {
        await _authService.StoreIssuedSecretAsync("valid_secret_123");
        _mockSignalR.IsConnected = true;

        var service = CreateService();
        var result = await service.SendHeartbeatAsync(_testIdentity);

        Assert.True(result);
        Assert.Single(_mockSignalR.SentHeartbeats);
        Assert.Empty(_mockApi.CapturedHeartbeats); // REST fallback was NOT triggered
        Assert.Equal("SignalR", _mockSignalR.SentHeartbeats[0].ConnectionTransport);
    }

    [Fact]
    public async Task SendHeartbeatAsync_SignalRDisconnected_FallsBackToRestApi()
    {
        await _authService.StoreIssuedSecretAsync("valid_secret_123");
        _mockSignalR.IsConnected = false; // SignalR offline

        var service = CreateService();
        var result = await service.SendHeartbeatAsync(_testIdentity);

        Assert.True(result);
        Assert.Empty(_mockSignalR.SentHeartbeats);
        Assert.Single(_mockApi.CapturedHeartbeats); // REST fallback engaged!
        Assert.Equal("REST", _mockApi.CapturedHeartbeats[0].ConnectionTransport);
    }

    [Fact]
    public async Task SendHeartbeatAsync_SignalRFails_FallsBackToRestApi()
    {
        await _authService.StoreIssuedSecretAsync("valid_secret_123");
        _mockSignalR.IsConnected = true;
        _mockSignalR.SimulateSendFailure = true; // SignalR invocation fails

        var service = CreateService();
        var result = await service.SendHeartbeatAsync(_testIdentity);

        Assert.True(result); // Recovered via REST
        Assert.Single(_mockApi.CapturedHeartbeats);
    }

    [Fact]
    public async Task SendHeartbeatAsync_BothSignalRAndRestFail_ReturnsFalseWithoutThrowing()
    {
        await _authService.StoreIssuedSecretAsync("valid_secret_123");
        _mockSignalR.IsConnected = false;
        _mockApi.OnSendHeartbeat = (payload, secret) => ApiResponse<HeartbeatAck>.Failure("Server unreachable", HttpStatusCode.ServiceUnavailable, isNetworkError: true);

        var service = CreateService();
        var result = await service.SendHeartbeatAsync(_testIdentity);

        Assert.False(result);
    }
}
