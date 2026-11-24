using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using GamingCafe.ClientAgent.Authentication;
using GamingCafe.ClientAgent.Communication;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Health;
using GamingCafe.ClientAgent.Heartbeat;
using GamingCafe.ClientAgent.Identity;
using GamingCafe.ClientAgent.Messaging;
using GamingCafe.ClientAgent.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GamingCafe.ClientAgent.Tests;

public class RealWebSocketConnectionTests : IDisposable
{
    private readonly X509Certificate2 _testCert;
    private WebApplication? _serverApp;
    private int _serverPort;
    private readonly string _tempDir;

    public RealWebSocketConnectionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AgentTest_WSS_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        // Generate in-memory X509 test certificate for localhost with Server Auth EKU and SAN
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("cn=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddIpAddress(IPAddress.Loopback);
        req.CertificateExtensions.Add(sanBuilder.Build());
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false)); // Server Authentication

        var ephemeralCert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var pfxBytes = ephemeralCert.Export(X509ContentType.Pfx, "testpassword");
        _testCert = X509CertificateLoader.LoadPkcs12(pfxBytes, "testpassword", X509KeyStorageFlags.Exportable);
    }

    [Fact]
    public async Task RealSignalR_OverWss_FullLifecycleVerification()
    {
        // 1. Spin up real in-process Kestrel SignalR Server over HTTPS/TLS
        await StartTestServerAsync();
        Assert.True(_serverPort > 0, "Server port should be allocated.");

        // 2. Setup Client Agent Services configured with WSS and pinned TLS cert thumbprint
        var config = new AgentConfiguration();
        config.Backend.HubUrl = $"wss://localhost:{_serverPort}/hubs/agent";
        config.Backend.RequireWss = true;
        config.Backend.ValidateCertificates = true;
        config.Backend.ExpectedCertificateThumbprint = _testCert.Thumbprint; // Pinned validation
        config.Identity.StorageDirectory = _tempDir;
        config.Security.RejectNonWss = true;
        config.Security.UseMachineScope = false;

        var configService = new MockConfigurationService(config);
        var secValidation = new SecurityValidationService(configService, NullLogger<SecurityValidationService>.Instance);

        var idStore = new DeviceIdentityStore(configService, NullLogger<DeviceIdentityStore>.Instance);
        var idService = new DeviceIdentityService(idStore, configService, NullLogger<DeviceIdentityService>.Instance);

        var protector = new DpapiCredentialProtection(configService, NullLogger<DpapiCredentialProtection>.Instance);
        var credStore = new SecureCredentialStore(configService, protector, NullLogger<SecureCredentialStore>.Instance);

        // Set test token
        var testDeviceId = idService.CurrentIdentity.DeviceId;
        credStore.SaveCredentials(new DeviceCredentials
        {
            DeviceId = testDeviceId,
            AccessToken = "test-jwt-access-token-12345",
            TokenExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2)
        });

        var authService = new DeviceAuthenticationService(credStore, NullLogger<DeviceAuthenticationService>.Instance);
        var connState = new ConnectionStateManager(NullLogger<ConnectionStateManager>.Instance);
        var reconnService = new ReconnectionService(configService, NullLogger<ReconnectionService>.Instance);
        var messageHandler = new MessageHandler(NullLogger<MessageHandler>.Instance);
        var healthService = new AgentHealthService();

        var connManager = new SignalRConnectionManager(
            configService,
            authService,
            secValidation,
            connState,
            reconnService,
            messageHandler,
            NullLogger<SignalRConnectionManager>.Instance);

        var heartbeatService = new HeartbeatService(
            connManager,
            idService,
            healthService,
            configService,
            NullLogger<HeartbeatService>.Instance);

        // 3. Connect Client to Server over WSS
        await connManager.StartAsync();
        Assert.True(connManager.IsConnected, "Client should be connected to server over WSS.");
        Assert.Equal(AgentConnectionStatus.Connected, connState.CurrentStatus);

        // 4. Verify Server authenticated client and WebSocket was used
        Assert.Equal(1, TestAgentHub.ConnectionCount);
        Assert.True(TestAgentHub.LastUsedWebSocket, "Connection must have used WebSocket transport.");
        Assert.Equal("Bearer test-jwt-access-token-12345", TestAgentHub.LastAuthorizationHeader);

        // 5. Send Heartbeat from Client to Server over WSS
        var heartbeatSent = await heartbeatService.SendHeartbeatAsync();
        Assert.True(heartbeatSent, "Heartbeat should be sent successfully.");

        // Wait up to 3 seconds for HeartbeatAck from server
        var ackReceived = await WaitForConditionAsync(() => TestAgentHub.HeartbeatReceivedCount > 0, TimeSpan.FromSeconds(3));
        Assert.True(ackReceived, "Server should have received heartbeat.");

        // 6. Server sends test message (DeviceConfiguration) to Client
        DeviceConfigurationMessage? receivedConfig = null;
        messageHandler.RegisterHandler<DeviceConfigurationMessage>("DeviceConfiguration", msg =>
        {
            receivedConfig = msg;
            return Task.CompletedTask;
        });

        await TestAgentHub.CurrentHubContext!.Clients.All.SendAsync("DeviceConfiguration", new DeviceConfigurationMessage
        {
            DeviceId = testDeviceId,
            ConfigVersion = "2.0.0-test",
            HeartbeatIntervalSeconds = 5
        });

        var configDelivered = await WaitForConditionAsync(() => receivedConfig != null, TimeSpan.FromSeconds(3));
        Assert.True(configDelivered, "Client should receive DeviceConfiguration from server.");
        Assert.Equal("2.0.0-test", receivedConfig?.ConfigVersion);

        // 7. Test Graceful Disconnect
        await connManager.StopAsync();
        Assert.False(connManager.IsConnected, "Client should be disconnected.");
        Assert.Equal(AgentConnectionStatus.Disconnected, connState.CurrentStatus);

        // 8. Stop the server
        await StopTestServerAsync();
    }

    [Fact]
    public async Task Reconnection_WhenServerRestarts_RestoresConnection()
    {
        // 1. Start Server
        await StartTestServerAsync();

        var config = new AgentConfiguration();
        config.Backend.HubUrl = $"wss://localhost:{_serverPort}/hubs/agent";
        config.Backend.RequireWss = true;
        config.Backend.ValidateCertificates = true;
        config.Backend.ExpectedCertificateThumbprint = _testCert.Thumbprint;
        config.Reconnection.InitialDelaySeconds = 0.5;
        config.Reconnection.MaxDelaySeconds = 2.0;
        config.Reconnection.BackoffMultiplier = 1.5;
        config.Reconnection.JitterMaxSeconds = 0.2;
        config.Identity.StorageDirectory = _tempDir;
        config.Security.RejectNonWss = true;
        config.Security.UseMachineScope = false;

        var configService = new MockConfigurationService(config);
        var secValidation = new SecurityValidationService(configService, NullLogger<SecurityValidationService>.Instance);
        var idStore = new DeviceIdentityStore(configService, NullLogger<DeviceIdentityStore>.Instance);
        var idService = new DeviceIdentityService(idStore, configService, NullLogger<DeviceIdentityService>.Instance);
        var protector = new DpapiCredentialProtection(configService, NullLogger<DpapiCredentialProtection>.Instance);
        var credStore = new SecureCredentialStore(configService, protector, NullLogger<SecureCredentialStore>.Instance);
        var authService = new DeviceAuthenticationService(credStore, NullLogger<DeviceAuthenticationService>.Instance);
        var connState = new ConnectionStateManager(NullLogger<ConnectionStateManager>.Instance);
        var reconnService = new ReconnectionService(configService, NullLogger<ReconnectionService>.Instance);
        var messageHandler = new MessageHandler(NullLogger<MessageHandler>.Instance);

        var connManager = new SignalRConnectionManager(
            configService,
            authService,
            secValidation,
            connState,
            reconnService,
            messageHandler,
            NullLogger<SignalRConnectionManager>.Instance);

        await connManager.StartAsync();
        Assert.True(connManager.IsConnected);

        // 2. Stop server to simulate server crash / network outage
        await StopTestServerAsync();

        // Verify client notices disconnection / reconnecting
        var disconnectedOrReconnecting = await WaitForConditionAsync(
            () => !connManager.IsConnected || connState.CurrentStatus == AgentConnectionStatus.Reconnecting,
            TimeSpan.FromSeconds(5));
        Assert.True(disconnectedOrReconnecting, "Client should detect server stoppage.");

        // 3. Restart server on the SAME port
        await StartTestServerAsync(_serverPort);

        // 4. Verify client automatically recovers and reconnects
        var reconnected = await WaitForConditionAsync(
            () => connManager.IsConnected && connState.CurrentStatus == AgentConnectionStatus.Connected,
            TimeSpan.FromSeconds(10));
        Assert.True(reconnected, "Client should automatically re-establish connection once server returns.");

        // Cleanup
        await connManager.StopAsync();
        await StopTestServerAsync();
    }

    private async Task StartTestServerAsync(int port = 0)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.Listen(IPAddress.Loopback, port, listenOptions =>
            {
                listenOptions.UseHttps(_testCert);
            });
        });

        builder.Services.AddSignalR();
        builder.Logging.AddConsole(); // show server logs for diagnosis

        _serverApp = builder.Build();
        _serverApp.MapHub<TestAgentHub>("/hubs/agent");

        await _serverApp.StartAsync();

        var server = _serverApp.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
        var address = addresses?.FirstOrDefault() ?? throw new InvalidOperationException("No server address found.");

        _serverPort = new Uri(address).Port;
        TestAgentHub.CurrentHubContext = _serverApp.Services.GetRequiredService<IHubContext<TestAgentHub>>();
    }

    private async Task StopTestServerAsync()
    {
        if (_serverApp != null)
        {
            await _serverApp.StopAsync();
            await _serverApp.DisposeAsync();
            _serverApp = null;
        }
    }

    private static async Task<bool> WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition()) return true;
            await Task.Delay(100);
        }
        return condition();
    }

    public void Dispose()
    {
        StopTestServerAsync().GetAwaiter().GetResult();
        _testCert.Dispose();
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

public class TestAgentHub : Hub
{
    public static int ConnectionCount;
    public static bool LastUsedWebSocket;
    public static string? LastAuthorizationHeader;
    public static int HeartbeatReceivedCount;
    public static IHubContext<TestAgentHub>? CurrentHubContext;

    public override async Task OnConnectedAsync()
    {
        Interlocked.Increment(ref ConnectionCount);

        var httpContext = Context.GetHttpContext();
        if (httpContext != null)
        {
            LastUsedWebSocket = httpContext.WebSockets.IsWebSocketRequest;
            LastAuthorizationHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Interlocked.Decrement(ref ConnectionCount);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendHeartbeat(HeartbeatMessage message)
    {
        Interlocked.Increment(ref HeartbeatReceivedCount);

        await Clients.Caller.SendAsync("HeartbeatAck", new HeartbeatAcknowledgment
        {
            DeviceId = message.DeviceId,
            SequenceNumber = message.SequenceNumber,
            OriginalMessageId = message.MessageId,
            ServerTimestampUtc = DateTimeOffset.UtcNow
        });
    }

    public async Task RegisterDevice(DeviceRegistrationMessage registration)
    {
        await Clients.Caller.SendAsync("DeviceApproval", new DeviceApprovalMessage
        {
            DeviceId = registration.DeviceId,
            IsApproved = true,
            AssignedZone = "Test-Zone"
        });
    }
}
