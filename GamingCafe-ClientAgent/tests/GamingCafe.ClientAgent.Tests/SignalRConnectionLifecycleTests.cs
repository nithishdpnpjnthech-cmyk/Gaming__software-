using System.Security.Cryptography.X509Certificates;
using GamingCafe.ClientAgent.Authentication;
using GamingCafe.ClientAgent.Communication;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Enrollment;
using GamingCafe.ClientAgent.Identity;
using GamingCafe.ClientAgent.Messaging;
using GamingCafe.ClientAgent.Security;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GamingCafe.ClientAgent.Tests;

public class SignalRConnectionLifecycleTests : IDisposable
{
    private readonly string _tempDir;
    private readonly AgentConfiguration _config;
    private readonly MockConfigurationService _configService;
    private readonly SecurityValidationService _secValidation;
    private readonly DeviceIdentityService _idService;
    private readonly DeviceAuthenticationService _authService;
    private readonly ConnectionStateManager _connState;
    private readonly ReconnectionService _reconnService;
    private readonly MessageHandler _messageHandler;

    public SignalRConnectionLifecycleTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AgentTest_Lifecycle_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _config = new AgentConfiguration();
        _config.Backend.HubUrl = "wss://localhost:5001/hubs/agent";
        _config.Backend.RequireWss = true;
        _config.Backend.ValidateCertificates = true;
        _config.Identity.StorageDirectory = _tempDir;
        _config.Security.RejectNonWss = true;
        _config.Security.UseMachineScope = false;

        _configService = new MockConfigurationService(_config);
        _secValidation = new SecurityValidationService(_configService, NullLogger<SecurityValidationService>.Instance);

        var idStore = new DeviceIdentityStore(_configService, NullLogger<DeviceIdentityStore>.Instance);
        _idService = new DeviceIdentityService(idStore, _configService, NullLogger<DeviceIdentityService>.Instance);

        var protector = new DpapiCredentialProtection(_configService, NullLogger<DpapiCredentialProtection>.Instance);
        var credStore = new SecureCredentialStore(_configService, protector, NullLogger<SecureCredentialStore>.Instance);
        _authService = new DeviceAuthenticationService(credStore, NullLogger<DeviceAuthenticationService>.Instance);

        _connState = new ConnectionStateManager(NullLogger<ConnectionStateManager>.Instance);
        _reconnService = new ReconnectionService(_configService, NullLogger<ReconnectionService>.Instance);
        _messageHandler = new MessageHandler(NullLogger<MessageHandler>.Instance);
    }

    [Fact]
    public async Task SendAsync_WhenDisconnected_ThrowsInvalidOperationException()
    {
        var manager = new SignalRConnectionManager(
            _configService,
            _authService,
            _secValidation,
            _connState,
            _reconnService,
            _messageHandler,
            NullLogger<SignalRConnectionManager>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.SendAsync("TestMethod", new { Test = "Data" }));
    }

    [Fact]
    public async Task InvokeAsync_WhenDisconnected_ThrowsInvalidOperationException()
    {
        var manager = new SignalRConnectionManager(
            _configService,
            _authService,
            _secValidation,
            _connState,
            _reconnService,
            _messageHandler,
            NullLogger<SignalRConnectionManager>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.InvokeAsync<string>("TestMethod", new { Test = "Data" }));
    }

    [Fact]
    public async Task StartAsync_WhenCancelled_SetsStatusToDisconnectedAndThrows()
    {
        var manager = new SignalRConnectionManager(
            _configService,
            _authService,
            _secValidation,
            _connState,
            _reconnService,
            _messageHandler,
            NullLogger<SignalRConnectionManager>.Instance);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled token

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.StartAsync(cts.Token));
        Assert.Equal(AgentConnectionStatus.Disconnected, _connState.CurrentStatus);
        Assert.False(manager.IsConnected);
    }

    [Fact]
    public async Task StopAsync_WhenNotConnected_CompletesGracefullyWithoutThrowing()
    {
        var manager = new SignalRConnectionManager(
            _configService,
            _authService,
            _secValidation,
            _connState,
            _reconnService,
            _messageHandler,
            NullLogger<SignalRConnectionManager>.Instance);

        // Stopping an unstarted/not connected manager must be safe and idempotent
        await manager.StopAsync();
        Assert.Equal(AgentConnectionStatus.Disconnected, _connState.CurrentStatus);
    }

    [Fact]
    public void ReconnectionPolicy_DelayIncreasesExponentially_WithJitterBounds()
    {
        _config.Reconnection.InitialDelaySeconds = 2.0;
        _config.Reconnection.BackoffMultiplier = 2.0;
        _config.Reconnection.MaxDelaySeconds = 30.0;
        _config.Reconnection.JitterMaxSeconds = 1.0;

        var delay1 = _reconnService.CalculateNextDelay(1);
        var delay2 = _reconnService.CalculateNextDelay(2);
        var delay3 = _reconnService.CalculateNextDelay(3);

        // Attempt 1: 2.0s + [0, 1]s jitter -> [2.0, 3.0]
        Assert.True(delay1.TotalSeconds >= 2.0 && delay1.TotalSeconds <= 3.0);

        // Attempt 2: 4.0s + [0, 1]s jitter -> [4.0, 5.0]
        Assert.True(delay2.TotalSeconds >= 4.0 && delay2.TotalSeconds <= 5.0);

        // Attempt 3: 8.0s + [0, 1]s jitter -> [8.0, 9.0]
        Assert.True(delay3.TotalSeconds >= 8.0 && delay3.TotalSeconds <= 9.0);

        // Verify progression: delay1 < delay2 < delay3
        Assert.True(delay1 < delay2);
        Assert.True(delay2 < delay3);
    }

    [Fact]
    public void ReconnectionPolicy_WhenMaxAttemptsExceeded_ReturnsNullToHalt()
    {
        _config.Reconnection.MaxReconnectAttempts = 3;

        var retryContext = new RetryContext
        {
            PreviousRetryCount = 3, // Already tried 3 times; next attempt would be 4 > MaxReconnectAttempts
            RetryReason = new Exception("Connection lost")
        };

        var nextDelay = _reconnService.NextRetryDelay(retryContext);
        Assert.Null(nextDelay);
    }

    [Fact]
    public async Task EnrollmentState_FullLifecycleTransitions_TrackCorrectly()
    {
        var store = new DeviceIdentityStore(_configService, NullLogger<DeviceIdentityStore>.Instance);
        var backendId = Guid.NewGuid();
        var identity = _idService.GetOrCreateIdentity();
        identity.BackendDeviceId = backendId;
        identity.RegistrationStatus = "PENDING";
        store.SaveIdentity(identity);

        var enrollment = new DeviceEnrollmentService(_idService, store, _authService, NullLogger<DeviceEnrollmentService>.Instance);
        await enrollment.InitializeStateAsync();
        Assert.Equal(EnrollmentState.PendingApproval, enrollment.CurrentState);

        // 2. Admin approves station -> Transitions to Approved
        await enrollment.HandleApprovalMessageAsync(new DeviceApprovalMessage
        {
            DeviceId = backendId,
            IsApproved = true,
            AssignedZone = "VIP-Zone"
        });
        Assert.Equal(EnrollmentState.Approved, enrollment.CurrentState);

        // 3. Admin issues credentials -> Transitions to Authenticated
        await enrollment.HandleAuthenticationMessageAsync(new DeviceAuthenticationMessage
        {
            DeviceId = backendId,
            AccessToken = "valid-jwt-token",
            TokenExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(4)
        });
        Assert.Equal(EnrollmentState.Authenticated, enrollment.CurrentState);

        // 4. Admin revokes station -> Invalidation and Revoked state
        await enrollment.HandleRevocationMessageAsync(new DeviceRevocationMessage
        {
            DeviceId = backendId,
            Reason = "Security compliance revocation"
        });
        Assert.Equal(EnrollmentState.Revoked, enrollment.CurrentState);
        Assert.Null(_authService.CurrentCredentials); // Credentials wiped
    }

    [Fact]
    public void DeviceRegistrationPersistence_PreservesBackendDeviceId_AcrossRestarts()
    {
        var store = new DeviceIdentityStore(_configService, NullLogger<DeviceIdentityStore>.Instance);
        var identity = _idService.GetOrCreateIdentity();
        var expectedBackendId = Guid.NewGuid();

        // Assign backend ID
        identity.BackendDeviceId = expectedBackendId;
        identity.RegistrationStatus = "PENDING";
        store.SaveIdentity(identity);

        // Simulate application restart with new store and service instances
        var restartedStore = new DeviceIdentityStore(_configService, NullLogger<DeviceIdentityStore>.Instance);
        var restartedService = new DeviceIdentityService(restartedStore, _configService, NullLogger<DeviceIdentityService>.Instance);

        var restoredIdentity = restartedService.CurrentIdentity;
        Assert.NotNull(restoredIdentity);
        Assert.Equal(identity.DeviceId, restoredIdentity.DeviceId);
        Assert.Equal(expectedBackendId, restoredIdentity.BackendDeviceId);
        Assert.Equal("PENDING", restoredIdentity.RegistrationStatus);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch { /* ignore */ }
    }
}
