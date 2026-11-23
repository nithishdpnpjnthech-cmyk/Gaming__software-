using System.Net;
using GamingCafe.ClientAgent.Authentication;
using GamingCafe.ClientAgent.Communication.ApiModels;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Enrollment;
using GamingCafe.ClientAgent.Identity;
using GamingCafe.ClientAgent.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GamingCafe.ClientAgent.Tests.Enrollment;

public class EnrollmentServiceTests
{
    private readonly TestCredentialStore _credentialStore = new();
    private readonly DeviceAuthenticationService _authService;
    private readonly MockBackendApiClient _mockApi = new();
    private readonly IOptions<AgentConfiguration> _options;
    private readonly DeviceIdentity _testIdentity;

    public EnrollmentServiceTests()
    {
        _authService = new DeviceAuthenticationService(_credentialStore, NullLogger<DeviceAuthenticationService>.Instance);
        var config = new AgentConfiguration
        {
            BackendApiBaseUrl = "https://127.0.0.1:5001",
            AgentVersion = "1.0.0-test"
        };
        _options = Microsoft.Extensions.Options.Options.Create(config);

        _testIdentity = new DeviceIdentity
        {
            DeviceId = Guid.NewGuid(),
            Hostname = "TEST-PC",
            OperatingSystem = "Windows 11",
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private EnrollmentService CreateService() =>
        new(_mockApi, _authService, _options, NullLogger<EnrollmentService>.Instance);

    [Fact]
    public async Task EvaluateInitialState_WithoutCredential_ReturnsUnregistered()
    {
        var service = CreateService();
        var state = await service.EvaluateInitialStateAsync(_testIdentity);

        Assert.Equal(EnrollmentState.Unregistered, state);
    }

    [Fact]
    public async Task EvaluateInitialState_WithCredential_ReturnsAuthenticated()
    {
        await _authService.StoreIssuedSecretAsync("valid_secret");
        var service = CreateService();

        var state = await service.EvaluateInitialStateAsync(_testIdentity);

        Assert.Equal(EnrollmentState.Authenticated, state);
    }

    [Fact]
    public async Task ProcessEnrollmentStep_RegistrationPending_TransitionsToPendingApproval()
    {
        _mockApi.OnRegister = req => ApiResponse<DeviceRegistrationResponse>.Success(new DeviceRegistrationResponse
        {
            DeviceId = _testIdentity.DeviceId,
            Status = "PendingApproval"
        });

        var service = CreateService();
        await service.EvaluateInitialStateAsync(_testIdentity);

        var state = await service.ProcessEnrollmentStepAsync(_testIdentity);

        Assert.Equal(EnrollmentState.PendingApproval, state);
    }

    [Fact]
    public async Task ProcessEnrollmentStep_RegistrationApproved_TransitionsToAuthenticated()
    {
        _mockApi.OnRegister = req => ApiResponse<DeviceRegistrationResponse>.Success(new DeviceRegistrationResponse
        {
            DeviceId = _testIdentity.DeviceId,
            Status = "Approved",
            DeviceSecret = "approved_token_123"
        });

        var service = CreateService();
        await service.EvaluateInitialStateAsync(_testIdentity);

        var state = await service.ProcessEnrollmentStepAsync(_testIdentity);

        Assert.Equal(EnrollmentState.Authenticated, state);
        Assert.Equal("approved_token_123", await _authService.GetDeviceSecretAsync());
    }

    [Fact]
    public async Task ProcessEnrollmentStep_StatusPollApproved_TransitionsToAuthenticated()
    {
        // 1. Initial register -> PendingApproval
        _mockApi.OnRegister = req => ApiResponse<DeviceRegistrationResponse>.Success(new DeviceRegistrationResponse
        {
            DeviceId = _testIdentity.DeviceId,
            Status = "PendingApproval"
        });
        var service = CreateService();
        await service.EvaluateInitialStateAsync(_testIdentity);
        await service.ProcessEnrollmentStepAsync(_testIdentity);

        // 2. Poll -> Approved
        _mockApi.OnGetStatus = id => ApiResponse<DeviceEnrollmentStatusResponse>.Success(new DeviceEnrollmentStatusResponse
        {
            DeviceId = id,
            Status = "Approved",
            DeviceSecret = "approved_secret_from_poll"
        });

        var newState = await service.ProcessEnrollmentStepAsync(_testIdentity);

        Assert.Equal(EnrollmentState.Authenticated, newState);
        Assert.Equal("approved_secret_from_poll", await _authService.GetDeviceSecretAsync());
    }

    [Fact]
    public async Task ProcessEnrollmentStep_StatusPollRejected_TransitionsToRejected()
    {
        _mockApi.OnRegister = req => ApiResponse<DeviceRegistrationResponse>.Success(new DeviceRegistrationResponse
        {
            DeviceId = _testIdentity.DeviceId,
            Status = "PendingApproval"
        });
        var service = CreateService();
        await service.EvaluateInitialStateAsync(_testIdentity);
        await service.ProcessEnrollmentStepAsync(_testIdentity);

        _mockApi.OnGetStatus = id => ApiResponse<DeviceEnrollmentStatusResponse>.Success(new DeviceEnrollmentStatusResponse
        {
            DeviceId = id,
            Status = "Rejected",
            Message = "Rejected by owner"
        });

        var newState = await service.ProcessEnrollmentStepAsync(_testIdentity);

        Assert.Equal(EnrollmentState.Rejected, newState);
        Assert.Null(await _authService.GetDeviceSecretAsync());
    }

    [Fact]
    public async Task HandleRevocationAsync_PurgesSecretAndTransitionsToRevoked()
    {
        await _authService.StoreIssuedSecretAsync("secret_to_revoke");
        var service = CreateService();
        await service.EvaluateInitialStateAsync(_testIdentity);
        Assert.Equal(EnrollmentState.Authenticated, service.CurrentState);

        await service.HandleRevocationAsync();

        Assert.Equal(EnrollmentState.Revoked, service.CurrentState);
        Assert.Null(await _authService.GetDeviceSecretAsync());
    }
}
