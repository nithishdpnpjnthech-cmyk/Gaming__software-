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

    public async Task<DeviceAuthResult> AuthenticateDeviceAsync(
        DeviceAuthRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var config = _configService.CurrentConfiguration;
        var baseUri = config.Backend.ApiBaseUrl.TrimEnd('/');
        var targetEndpoint = $"{baseUri}/api/devices/auth";

        _logger.LogDebug("Requesting device authentication token from '{Endpoint}' (DeviceCode: '{DeviceCode}')...",
            targetEndpoint, request.DeviceCode);

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

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var responseDto = JsonSerializer.Deserialize<DeviceAuthResponseDto>(responseBody, JsonOptions);
                _logger.LogInformation("Device {DeviceCode} successfully authenticated by backend! Token expires at {ExpiresAt}",
                    request.DeviceCode, responseDto?.ExpiresAt);

                return new DeviceAuthResult
                {
                    Status = DeviceAuthResultStatus.Success,
                    StatusCode = statusCode,
                    Data = responseDto,
                    ErrorMessage = null
                };
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                _logger.LogDebug("Device authentication forbidden (HTTP 403): {Body}", responseBody);

                var isPending = responseBody.Contains("awaiting administrator approval", StringComparison.OrdinalIgnoreCase);
                return new DeviceAuthResult
                {
                    Status = isPending ? DeviceAuthResultStatus.PendingApproval : DeviceAuthResultStatus.Denied,
                    StatusCode = statusCode,
                    ErrorMessage = responseBody
                };
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Device authentication unauthorized (HTTP 401): {Body}", responseBody);

                return new DeviceAuthResult
                {
                    Status = DeviceAuthResultStatus.Denied,
                    StatusCode = statusCode,
                    ErrorMessage = responseBody
                };
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Device authentication not found (HTTP 404): {Body}", responseBody);

                return new DeviceAuthResult
                {
                    Status = DeviceAuthResultStatus.NotFound,
                    StatusCode = statusCode,
                    ErrorMessage = responseBody
                };
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                _logger.LogError("Device authentication validation error (HTTP 400): {Body}", responseBody);

                return new DeviceAuthResult
                {
                    Status = DeviceAuthResultStatus.ValidationError,
                    StatusCode = statusCode,
                    ErrorMessage = responseBody
                };
            }

            _logger.LogWarning("Unexpected response from backend device auth endpoint: HTTP {StatusCode} {Body}",
                statusCode, responseBody);

            return new DeviceAuthResult
            {
                Status = statusCode >= 500 ? DeviceAuthResultStatus.ServerError : DeviceAuthResultStatus.UnexpectedResponse,
                StatusCode = statusCode,
                ErrorMessage = responseBody
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Device authentication request cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            var (category, reason) = ConnectionErrorClassifier.Classify(ex);
            _logger.LogWarning("Failed to reach backend device auth endpoint [{Category}]: {Reason}", category, reason);

            return new DeviceAuthResult
            {
                Status = DeviceAuthResultStatus.NetworkFailure,
                ErrorMessage = $"{category}: {reason}"
            };
        }
    }
}
