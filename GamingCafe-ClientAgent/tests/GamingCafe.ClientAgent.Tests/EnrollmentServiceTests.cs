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
    private readonly DeviceIdentityStore _identityStore;
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

        _identityStore = new DeviceIdentityStore(_mockConfig, NullLogger<DeviceIdentityStore>.Instance);
        _identityService = new DeviceIdentityService(_identityStore, _mockConfig, NullLogger<DeviceIdentityService>.Instance);

        var protector = new DpapiCredentialProtection(_mockConfig, NullLogger<DpapiCredentialProtection>.Instance);
        var credStore = new SecureCredentialStore(_mockConfig, protector, NullLogger<SecureCredentialStore>.Instance);
        _authService = new DeviceAuthenticationService(credStore, NullLogger<DeviceAuthenticationService>.Instance);
    }

    [Fact]
    public async Task InitializeState_WhenNoCredentials_SetsUnregistered()
    {
        var enrollment = new DeviceEnrollmentService(_identityService, _identityStore, _authService, NullLogger<DeviceEnrollmentService>.Instance);
        await enrollment.InitializeStateAsync();

        Assert.Equal(EnrollmentState.Unregistered, enrollment.CurrentState);
    }

    [Fact]
    public async Task InitializeState_WhenBackendDeviceIdPresent_SetsPendingApproval()
    {
        var identity = _identityService.GetOrCreateIdentity();
        identity.BackendDeviceId = Guid.NewGuid();
        identity.RegistrationStatus = "PENDING";
        _identityStore.SaveIdentity(identity);

        var enrollment = new DeviceEnrollmentService(_identityService, _identityStore, _authService, NullLogger<DeviceEnrollmentService>.Instance);
        await enrollment.InitializeStateAsync();

        Assert.Equal(EnrollmentState.PendingApproval, enrollment.CurrentState);
    }

    [Fact]
    public async Task InitializeState_WhenNoBackendDeviceId_EvenWithConflict_SetsErrorState()
    {
        var identity = _identityService.GetOrCreateIdentity();
        identity.BackendDeviceId = null;
        identity.RegistrationStatus = "CONFLICT";
        _identityStore.SaveIdentity(identity);

        var enrollment = new DeviceEnrollmentService(_identityService, _identityStore, _authService, NullLogger<DeviceEnrollmentService>.Instance);
        await enrollment.InitializeStateAsync();

        // Must transition to Error state and NOT assume approval or authentication
        Assert.Equal(EnrollmentState.Error, enrollment.CurrentState);
        Assert.NotEqual(EnrollmentState.Approved, enrollment.CurrentState);
        Assert.NotEqual(EnrollmentState.Authenticated, enrollment.CurrentState);
        Assert.NotEqual(EnrollmentState.PendingApproval, enrollment.CurrentState);
    }

    [Fact]
    public async Task HandleApprovalMessage_WhenApprovedWithCorrectBackendDeviceId_TransitionsToApprovedAndPersistsState()
    {
        var backendId = Guid.NewGuid();
        var identity = _identityService.GetOrCreateIdentity();
        identity.BackendDeviceId = backendId;
        identity.RegistrationStatus = "PENDING";
        _identityStore.SaveIdentity(identity);

        var enrollment = new DeviceEnrollmentService(_identityService, _identityStore, _authService, NullLogger<DeviceEnrollmentService>.Instance);
        await enrollment.InitializeStateAsync();
        Assert.Equal(EnrollmentState.PendingApproval, enrollment.CurrentState);

        var approval = new DeviceApprovalMessage
        {
            DeviceId = backendId, // Valid Backend Device UUID
            IsApproved = true,
            AssignedZone = "Standard"
        };

        await enrollment.HandleApprovalMessageAsync(approval);
        Assert.Equal(EnrollmentState.Approved, enrollment.CurrentState);

        // Verify state is persisted in identity store
        var savedIdentity = _identityStore.LoadIdentity();
        Assert.NotNull(savedIdentity);
        Assert.Equal("APPROVED", savedIdentity.RegistrationStatus);
        Assert.Equal(backendId, savedIdentity.BackendDeviceId);

        // Verify recovery after application restart
        var restartedEnrollment = new DeviceEnrollmentService(_identityService, _identityStore, _authService, NullLogger<DeviceEnrollmentService>.Instance);
        await restartedEnrollment.InitializeStateAsync();
        Assert.Equal(EnrollmentState.Approved, restartedEnrollment.CurrentState);
    }

    [Fact]
    public async Task HandleApprovalMessage_WhenMismatchedUUID_RejectsMessageAndRemainsInPendingApproval()
    {
        var backendId = Guid.NewGuid();
        var localId = _identityService.CurrentIdentity.DeviceId;
        var identity = _identityService.GetOrCreateIdentity();
        identity.BackendDeviceId = backendId;
        identity.RegistrationStatus = "PENDING";
        _identityStore.SaveIdentity(identity);

        var enrollment = new DeviceEnrollmentService(_identityService, _identityStore, _authService, NullLogger<DeviceEnrollmentService>.Instance);
        await enrollment.InitializeStateAsync();

        // Server sends approval with Local DeviceId or completely foreign UUID instead of BackendDeviceId
        var wrongApproval = new DeviceApprovalMessage
        {
            DeviceId = localId, // Accidental Local DeviceId comparison check
            IsApproved = true,
            AssignedZone = "Standard"
        };

        await enrollment.HandleApprovalMessageAsync(wrongApproval);

        // State MUST remain PendingApproval because DeviceId did not match BackendDeviceId
        Assert.Equal(EnrollmentState.PendingApproval, enrollment.CurrentState);
        var stored = _identityStore.LoadIdentity();
        Assert.Equal("PENDING", stored?.RegistrationStatus);
    }

    [Fact]
    public async Task HandleApprovalMessage_WhenDuplicateApproved_DoesNotCorruptState()
    {
        var backendId = Guid.NewGuid();
        var identity = _identityService.GetOrCreateIdentity();
        identity.BackendDeviceId = backendId;
        identity.RegistrationStatus = "PENDING";
        _identityStore.SaveIdentity(identity);

        var enrollment = new DeviceEnrollmentService(_identityService, _identityStore, _authService, NullLogger<DeviceEnrollmentService>.Instance);
        await enrollment.InitializeStateAsync();

        var approval = new DeviceApprovalMessage
        {
            DeviceId = backendId,
            IsApproved = true,
            AssignedZone = "Standard"
        };

        await enrollment.HandleApprovalMessageAsync(approval);
        Assert.Equal(EnrollmentState.Approved, enrollment.CurrentState);

        // Send duplicate approval notification
        await enrollment.HandleApprovalMessageAsync(approval);
        Assert.Equal(EnrollmentState.Approved, enrollment.CurrentState);
        var stored = _identityStore.LoadIdentity();
        Assert.Equal("APPROVED", stored?.RegistrationStatus);
    }

    [Fact]
    public async Task HandleAuthenticationMessage_SavesCredentials_TransitionsToAuthenticated()
    {
        var backendId = Guid.NewGuid();
        var identity = _identityService.GetOrCreateIdentity();
        identity.BackendDeviceId = backendId;
        identity.RegistrationStatus = "APPROVED";
        _identityStore.SaveIdentity(identity);

        var enrollment = new DeviceEnrollmentService(_identityService, _identityStore, _authService, NullLogger<DeviceEnrollmentService>.Instance);
        await enrollment.InitializeStateAsync();
        Assert.Equal(EnrollmentState.Approved, enrollment.CurrentState);

        var authMsg = new DeviceAuthenticationMessage
        {
            DeviceId = backendId,
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
        var backendId = Guid.NewGuid();
        var identity = _identityService.GetOrCreateIdentity();
        identity.BackendDeviceId = backendId;
        identity.RegistrationStatus = "APPROVED";
        _identityStore.SaveIdentity(identity);

        var enrollment = new DeviceEnrollmentService(_identityService, _identityStore, _authService, NullLogger<DeviceEnrollmentService>.Instance);
        await enrollment.InitializeStateAsync();
        Assert.Equal(EnrollmentState.Approved, enrollment.CurrentState);
        
        // Setup initial auth
        var authMsg = new DeviceAuthenticationMessage
        {
            DeviceId = backendId,
            AccessToken = "jwt-revocable-token",
            TokenExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(4)
        };
        await enrollment.HandleAuthenticationMessageAsync(authMsg);
        Assert.True(_authService.IsAuthenticated);
        Assert.Equal(EnrollmentState.Authenticated, enrollment.CurrentState);

        // Revoke
        var revokeMsg = new DeviceRevocationMessage
        {
            DeviceId = backendId,
            Reason = "PC decommissioned"
        };
        await enrollment.HandleRevocationMessageAsync(revokeMsg);

        Assert.Equal(EnrollmentState.Revoked, enrollment.CurrentState);
        Assert.False(_authService.IsAuthenticated);
        Assert.Null(_authService.CurrentCredentials);

        var stored = _identityStore.LoadIdentity();
        Assert.Equal("REVOKED", stored?.RegistrationStatus);
    }

    [Fact]
    public async Task InvalidStateTransitions_AreRejected()
    {
        var enrollment = new DeviceEnrollmentService(_identityService, _identityStore, _authService, NullLogger<DeviceEnrollmentService>.Instance);
        await enrollment.InitializeStateAsync();
        Assert.Equal(EnrollmentState.Unregistered, enrollment.CurrentState);

        // Attempt direct jump from Unregistered to Authenticated (invalid)
        await enrollment.TransitionToAsync(EnrollmentState.Authenticated);
        Assert.Equal(EnrollmentState.Unregistered, enrollment.CurrentState);

        // Transition from Revoked to RegistrationPending (terminal state, invalid)
        await enrollment.TransitionToAsync(EnrollmentState.PendingApproval);
        await enrollment.TransitionToAsync(EnrollmentState.Revoked);
        Assert.Equal(EnrollmentState.Revoked, enrollment.CurrentState);

        await enrollment.TransitionToAsync(EnrollmentState.RegistrationPending);
        Assert.Equal(EnrollmentState.Revoked, enrollment.CurrentState);
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
