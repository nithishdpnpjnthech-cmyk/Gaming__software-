using System.Net;
using System.Text;
using System.Text.Json;
using GamingCafe.ClientAgent.Communication;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GamingCafe.ClientAgent.Tests;

public class BackendApiClientTests
{
    private readonly AgentConfiguration _config;
    private readonly MockConfigurationService _configService;
    private readonly ISecurityValidationService _securityValidation;

    public BackendApiClientTests()
    {
        _config = new AgentConfiguration();
        _config.Backend.ApiBaseUrl = "https://backend.gamingcafe.local:7267";
        _configService = new MockConfigurationService(_config);
        _securityValidation = new SecurityValidationService(_configService, NullLogger<SecurityValidationService>.Instance);
    }

    [Fact]
    public async Task RegisterDeviceAsync_When201Created_ReturnsSuccessWithBackendId()
    {
        var expectedId = Guid.NewGuid();
        var responsePayload = new
        {
            id = expectedId,
            deviceCode = "PC-01",
            displayName = "Gaming Station 01",
            registrationStatus = "PENDING",
            message = "Device registered successfully and is awaiting admin approval."
        };

        var handler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(JsonSerializer.Serialize(responsePayload), Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler);
        var client = new BackendApiClient(httpClient, _configService, _securityValidation, NullLogger<BackendApiClient>.Instance);

        var request = new DeviceRegistrationRequestDto
        {
            DeviceCode = "PC-01",
            DisplayName = "Gaming Station 01",
            Hostname = "GAMING-PC-01",
            HardwareFingerprintHash = "abcdef123456",
            AgentVersion = "1.0.0"
        };

        var result = await client.RegisterDeviceAsync(request);

        Assert.Equal(RegistrationResultStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(expectedId, result.Data.Id);
        Assert.Equal("PC-01", result.Data.DeviceCode);
        Assert.Equal("PENDING", result.Data.RegistrationStatus);
        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("https://backend.gamingcafe.local:7267/api/devices/register", handler.LastRequest?.RequestUri?.ToString());
    }

    [Fact]
    public async Task RegisterDeviceAsync_When409Conflict_ReturnsAlreadyExistsWithoutApproving()
    {
        var conflictMessage = "A device with this device code already exists.";
        var handler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(conflictMessage, Encoding.UTF8, "text/plain")
        });

        var httpClient = new HttpClient(handler);
        var client = new BackendApiClient(httpClient, _configService, _securityValidation, NullLogger<BackendApiClient>.Instance);

        var request = new DeviceRegistrationRequestDto
        {
            DeviceCode = "PC-01",
            DisplayName = "Gaming Station 01"
        };

        var result = await client.RegisterDeviceAsync(request);

        Assert.Equal(RegistrationResultStatus.AlreadyExists, result.Status);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("already exists", result.ErrorMessage);
        Assert.Null(result.Data); // 409 returns no backend entity or device ID; ownership cannot be assumed
    }

    [Fact]
    public async Task RegisterDeviceAsync_When400BadRequest_ReturnsValidationError()
    {
        var validationMessage = "Device code and display name are required.";
        var handler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(validationMessage, Encoding.UTF8, "text/plain")
        });

        var httpClient = new HttpClient(handler);
        var client = new BackendApiClient(httpClient, _configService, _securityValidation, NullLogger<BackendApiClient>.Instance);

        var request = new DeviceRegistrationRequestDto();

        var result = await client.RegisterDeviceAsync(request);

        Assert.Equal(RegistrationResultStatus.ValidationError, result.Status);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("Device code and display name are required", result.ErrorMessage);
    }

    [Fact]
    public async Task RegisterDeviceAsync_When500ServerError_ReturnsServerError()
    {
        var handler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Internal database fault.", Encoding.UTF8, "text/plain")
        });

        var httpClient = new HttpClient(handler);
        var client = new BackendApiClient(httpClient, _configService, _securityValidation, NullLogger<BackendApiClient>.Instance);

        var request = new DeviceRegistrationRequestDto
        {
            DeviceCode = "PC-01",
            DisplayName = "Station"
        };

        var result = await client.RegisterDeviceAsync(request);

        Assert.Equal(RegistrationResultStatus.ServerError, result.Status);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task RegisterDeviceAsync_WhenNetworkFails_ReturnsNetworkFailure()
    {
        var handler = new MockHttpMessageHandler(new HttpRequestException("Connection refused by target host."));

        var httpClient = new HttpClient(handler);
        var client = new BackendApiClient(httpClient, _configService, _securityValidation, NullLogger<BackendApiClient>.Instance);

        var request = new DeviceRegistrationRequestDto
        {
            DeviceCode = "PC-01",
            DisplayName = "Station"
        };

        var result = await client.RegisterDeviceAsync(request);

        Assert.Equal(RegistrationResultStatus.NetworkFailure, result.Status);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task RegisterDeviceAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var handler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        var client = new BackendApiClient(httpClient, _configService, _securityValidation, NullLogger<BackendApiClient>.Instance);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await client.RegisterDeviceAsync(new DeviceRegistrationRequestDto(), cts.Token);
        });
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage? _response;
        private readonly Exception? _exception;

        public HttpRequestMessage? LastRequest { get; private set; }

        public MockHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        public MockHttpMessageHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;

            if (_exception != null)
            {
                throw _exception;
            }

            return Task.FromResult(_response ?? new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
