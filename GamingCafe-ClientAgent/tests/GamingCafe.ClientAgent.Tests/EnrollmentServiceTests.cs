using GamingCafe.ClientAgent.Authentication;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Enrollment;
using GamingCafe.ClientAgent.Identity;
using GamingCafe.ClientAgent.Messaging;
using GamingCafe.ClientAgent.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GamingCafe.ClientAgent.Tests;

public class EnrollmentServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly IConfigurationService _mockConfig;
    private readonly DeviceIdentityService _identityService;
    private readonly DeviceAuthenticationService _authService;

    public EnrollmentServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "AgentTest_Enroll_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);

        var config = new AgentConfiguration();
        config.Identity.StorageDirectory = _tempDirectory;
        config.Security.UseMachineScope = false;
        _mockConfig = new MockConfigurationService(config);

        var idStore = new DeviceIdentityStore(_mockConfig, NullLogger<DeviceIdentityStore>.Instance);
        _identityService = new DeviceIdentityService(idStore, _mockConfig, NullLogger<DeviceIdentityService>.Instance);

        var protector = new DpapiCredentialProtection(_mockConfig, NullLogger<DpapiCredentialProtection>.Instance);
        var credStore = new SecureCredentialStore(_mockConfig, protector, NullLogger<SecureCredentialStore>.Instance);
        _authService = new DeviceAuthenticationService(credStore, NullLogger<DeviceAuthenticationService>.Instance);
    }

    [Fact]
    public async Task InitializeState_WhenNoCredentials_SetsUnregistered()
    {
        var enrollment = new DeviceEnrollmentService(_identityService, _authService, NullLogger<DeviceEnrollmentService>.Instance);
        await enrollment.InitializeStateAsync();

        Assert.Equal(EnrollmentState.Unregistered, enrollment.CurrentState);
    }

    [Fact]
    public async Task HandleApprovalMessage_WhenApproved_TransitionsToApproved()
    {
        var enrollment = new DeviceEnrollmentService(_identityService, _authService, NullLogger<DeviceEnrollmentService>.Instance);
        await enrollment.InitializeStateAsync();

        var approval = new DeviceApprovalMessage
        {
            DeviceId = _identityService.CurrentIdentity.DeviceId,
            IsApproved = true,
            AssignedZone = "Standard"
        };

        await enrollment.HandleApprovalMessageAsync(approval);
        Assert.Equal(EnrollmentState.Approved, enrollment.CurrentState);
    }

    [Fact]
    public async Task HandleAuthenticationMessage_SavesCredentials_TransitionsToAuthenticated()
    {
        var enrollment = new DeviceEnrollmentService(_identityService, _authService, NullLogger<DeviceEnrollmentService>.Instance);
        await enrollment.InitializeStateAsync();

        var authMsg = new DeviceAuthenticationMessage
        {
            DeviceId = _identityService.CurrentIdentity.DeviceId,
            AccessToken = "jwt-mock-token-xyz",
            TokenExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(4)
        };

        await enrollment.HandleAuthenticationMessageAsync(authMsg);

        Assert.Equal(EnrollmentState.Authenticated, enrollment.CurrentState);
        Assert.True(_authService.IsAuthenticated);
        Assert.Equal("jwt-mock-token-xyz", _authService.CurrentCredentials?.AccessToken);
    }

    [Fact]
    public async Task HandleRevocationMessage_ClearsCredentials_TransitionsToRevoked()
    {
        var enrollment = new DeviceEnrollmentService(_identityService, _authService, NullLogger<DeviceEnrollmentService>.Instance);
        
        // Setup initial auth
        var authMsg = new DeviceAuthenticationMessage
        {
            DeviceId = _identityService.CurrentIdentity.DeviceId,
            AccessToken = "jwt-revocable-token",
            TokenExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(4)
        };
        await enrollment.HandleAuthenticationMessageAsync(authMsg);
        Assert.True(_authService.IsAuthenticated);

        // Revoke
        var revokeMsg = new DeviceRevocationMessage
        {
            DeviceId = _identityService.CurrentIdentity.DeviceId,
            Reason = "PC decommissioned"
        };
        await enrollment.HandleRevocationMessageAsync(revokeMsg);

        Assert.Equal(EnrollmentState.Revoked, enrollment.CurrentState);
        Assert.False(_authService.IsAuthenticated);
        Assert.Null(_authService.CurrentCredentials);
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
}
