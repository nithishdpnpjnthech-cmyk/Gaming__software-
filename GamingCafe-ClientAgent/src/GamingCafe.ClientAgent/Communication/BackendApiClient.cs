using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Security;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Communication;

public sealed class BackendApiClient : IBackendApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfigurationService _configService;
    private readonly ISecurityValidationService _securityValidation;
    private readonly ILogger<BackendApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public BackendApiClient(
        HttpClient httpClient,
        IConfigurationService configService,
        ISecurityValidationService securityValidation,
        ILogger<BackendApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _securityValidation = securityValidation ?? throw new ArgumentNullException(nameof(securityValidation));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DeviceRegistrationResult> RegisterDeviceAsync(
        DeviceRegistrationRequestDto request, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var config = _configService.CurrentConfiguration;
        var baseUri = config.Backend.ApiBaseUrl.TrimEnd('/');
        var targetEndpoint = $"{baseUri}/api/devices/register";

        _logger.LogInformation("Submitting device registration to backend at '{Endpoint}' (DeviceCode: '{DeviceCode}', DisplayName: '{DisplayName}')...",
            targetEndpoint, request.DeviceCode, request.DisplayName);

        try
        {
            var jsonPayload = JsonSerializer.Serialize(request, JsonOptions);
            using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, targetEndpoint)
            {
                Content = content
            };
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var statusCode = (int)response.StatusCode;
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Backend registration response received: HTTP {StatusCode}. Body: {Body}", statusCode, responseBody);

            if (response.StatusCode == HttpStatusCode.Created)
            {
                var responseDto = JsonSerializer.Deserialize<DeviceRegistrationResponseDto>(responseBody, JsonOptions);
                _logger.LogInformation("Device successfully registered in backend! Backend DeviceId: {Id}, Status: {Status}",
                    responseDto?.Id, responseDto?.RegistrationStatus);

                return new DeviceRegistrationResult
                {
                    Status = RegistrationResultStatus.Success,
                    StatusCode = statusCode,
                    Data = responseDto,
                    ErrorMessage = null
                };
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                _logger.LogWarning("Registration conflict (HTTP 409): {Body}. The device code '{DeviceCode}' is already registered in backend.",
                    responseBody, request.DeviceCode);

                return new DeviceRegistrationResult
                {
                    Status = RegistrationResultStatus.AlreadyExists,
                    StatusCode = statusCode,
                    ErrorMessage = responseBody
                };
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                _logger.LogError("Registration validation error (HTTP 400): {Body}", responseBody);

                return new DeviceRegistrationResult
                {
                    Status = RegistrationResultStatus.ValidationError,
                    StatusCode = statusCode,
                    ErrorMessage = responseBody
                };
            }

            _logger.LogWarning("Unexpected response from backend registration endpoint: HTTP {StatusCode} {Body}",
                statusCode, responseBody);

            return new DeviceRegistrationResult
            {
                Status = statusCode >= 500 ? RegistrationResultStatus.ServerError : RegistrationResultStatus.UnexpectedResponse,
                StatusCode = statusCode,
                ErrorMessage = responseBody
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Device registration cancelled due to cancellation request.");
            throw;
        }
        catch (Exception ex)
        {
            var (category, reason) = ConnectionErrorClassifier.Classify(ex);
            _logger.LogWarning("Failed to reach backend registration endpoint [{Category}]: {Reason}", category, reason);
            _logger.LogDebug(ex, "Detailed exception during device registration HTTP dispatch:");

            return new DeviceRegistrationResult
            {
                Status = RegistrationResultStatus.NetworkFailure,
                ErrorMessage = $"{category}: {reason}"
            };
        }
    }

    public async Task<DeviceVerificationResult> VerifyDeviceExistsAsync(
        Guid backendDeviceId,
        CancellationToken cancellationToken = default)
    {
        if (backendDeviceId == Guid.Empty)
        {
            return new DeviceVerificationResult
            {
                Status = DeviceVerificationStatus.NotFound,
                ErrorMessage = "BackendDeviceId is empty GUID."
            };
        }

        var config = _configService.CurrentConfiguration;
        var baseUri = config.Backend.ApiBaseUrl.TrimEnd('/');
        var targetEndpoint = $"{baseUri}/api/devices/{backendDeviceId}";

        _logger.LogInformation("Verifying device existence with backend at '{Endpoint}'...", targetEndpoint);

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, targetEndpoint);
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var statusCode = (int)response.StatusCode;

            if (response.StatusCode == HttpStatusCode.OK)
            {
                _logger.LogInformation("Backend verified: Device record '{BackendDeviceId}' exists in backend database.", backendDeviceId);
                return new DeviceVerificationResult
                {
                    Status = DeviceVerificationStatus.Confirmed,
                    StatusCode = statusCode
                };
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Backend reported HTTP 404 Not Found: Device record '{BackendDeviceId}' does NOT exist in backend database.", backendDeviceId);
                return new DeviceVerificationResult
                {
                    Status = DeviceVerificationStatus.NotFound,
                    StatusCode = statusCode,
                    ErrorMessage = "Device not found on backend."
                };
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                // Endpoint requires Authorization policy (devices.view)
                // 401/403 proves the backend is reachable and functioning, but does NOT indicate the device is missing.
                _logger.LogDebug("Verification endpoint requires authorization (HTTP {StatusCode}). Backend is alive; cannot confirm or deny record existence via unauthenticated GET.", statusCode);
                return new DeviceVerificationResult
                {
                    Status = DeviceVerificationStatus.Unauthorized,
                    StatusCode = statusCode,
                    ErrorMessage = $"Authorization required: HTTP {statusCode}"
                };
            }

            _logger.LogWarning("Verification check returned status HTTP {StatusCode}", statusCode);
            return new DeviceVerificationResult
            {
                Status = statusCode >= 500 ? DeviceVerificationStatus.ServerError : DeviceVerificationStatus.ServerError,
                StatusCode = statusCode,
                ErrorMessage = $"HTTP {statusCode}"
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var (category, reason) = ConnectionErrorClassifier.Classify(ex);
            _logger.LogWarning("Could not verify device existence at backend [{Category}]: {Reason}", category, reason);

            return new DeviceVerificationResult
            {
                Status = DeviceVerificationStatus.NetworkFailure,
                ErrorMessage = $"{category}: {reason}"
            };
        }
    }
}
