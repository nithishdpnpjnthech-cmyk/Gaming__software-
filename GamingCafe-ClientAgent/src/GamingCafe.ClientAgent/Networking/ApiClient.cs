using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Interfaces;
using GamingCafe.ClientAgent.Models.Dto;
using GamingCafe.ClientAgent.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GamingCafe.ClientAgent.Networking;

/// <summary>
/// Secure HTTP client abstraction for communication with the Admin backend.
/// Uses strict TLS validation, configurable timeouts, and comprehensive error mapping.
/// </summary>
public class ApiClient : IApiClient
{
    public const string HttpClientName = "AdminApiClient";

    // Proposed API Contract Endpoints (to be confirmed with backend team)
    public const string EndpointRegister = "/api/v1/devices/register";
    public const string EndpointEnrollmentStatus = "/api/v1/devices/{0}/status";
    public const string EndpointHeartbeat = "/api/v1/devices/heartbeat";

    private readonly HttpClient _httpClient;
    private readonly IOptions<AgentOptions> _options;
    private readonly IHealthTracker _healthTracker;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<AgentOptions> options,
        IHealthTracker healthTracker,
        ILogger<ApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient(HttpClientName);
        _options = options;
        _healthTracker = healthTracker;
        _logger = logger;
    }

    public async Task<ApiResponse<DeviceRegistrationResponse>> RegisterDeviceAsync(
        DeviceRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending device registration request for DeviceId: {DeviceId}, Hostname: {Hostname}",
            request.DeviceId, request.Hostname);

        return await SendJsonPostAsync<DeviceRegistrationRequest, DeviceRegistrationResponse>(
            EndpointRegister,
            request,
            deviceSecret: null,
            cancellationToken);
    }

    public async Task<ApiResponse<DeviceEnrollmentStatusResponse>> CheckEnrollmentStatusAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var endpoint = string.Format(EndpointEnrollmentStatus, deviceId);
        _logger.LogDebug("Checking enrollment status at {Endpoint}", endpoint);

        return await SendJsonGetAsync<DeviceEnrollmentStatusResponse>(endpoint, deviceSecret: null, cancellationToken);
    }

    public async Task<ApiResponse<HeartbeatResponse>> SendHeartbeatAsync(
        HeartbeatRequest request,
        string deviceSecret,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceSecret))
        {
            throw new ArgumentException("Heartbeat requires a valid device secret / credential.", nameof(deviceSecret));
        }

        _logger.LogDebug("Sending heartbeat for DeviceId: {DeviceId}, Uptime: {Uptime}s",
            request.DeviceId, request.UptimeSeconds);

        return await SendJsonPostAsync<HeartbeatRequest, HeartbeatResponse>(
            EndpointHeartbeat,
            request,
            deviceSecret,
            cancellationToken);
    }

    private async Task<ApiResponse<TResponse>> SendJsonPostAsync<TRequest, TResponse>(
        string relativeUrl,
        TRequest payload,
        string? deviceSecret,
        CancellationToken cancellationToken)
    {
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, relativeUrl);
            var json = JsonSerializer.Serialize(payload);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            if (!string.IsNullOrWhiteSpace(deviceSecret))
            {
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceSecret);
            }

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            _healthTracker.RecordConnectionAttempt(true);

            return await HandleHttpResponseAsync<TResponse>(response, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("API request to {Url} cancelled by service shutdown.", relativeUrl);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            // Timeout occurred
            _healthTracker.RecordConnectionAttempt(false);
            _logger.LogWarning("Request to {Url} timed out after {Timeout}s: {Message}",
                relativeUrl, _httpClient.Timeout.TotalSeconds, ex.Message);
            return ApiResponse<TResponse>.Failure("Connection timed out.", HttpStatusCode.RequestTimeout, isNetworkError: true);
        }
        catch (HttpRequestException ex)
        {
            _healthTracker.RecordConnectionAttempt(false);
            _logger.LogWarning("Network or connection failure contacting Admin API at {Url}: {Message}",
                relativeUrl, ex.Message);
            return ApiResponse<TResponse>.Failure($"Network error: {ex.Message}", ex.StatusCode ?? HttpStatusCode.ServiceUnavailable, isNetworkError: true);
        }
        catch (Exception ex)
        {
            _healthTracker.RecordConnectionAttempt(false);
            _logger.LogError(ex, "Unexpected error communicating with Admin API at {Url}", relativeUrl);
            return ApiResponse<TResponse>.Failure($"Unexpected error: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    private async Task<ApiResponse<TResponse>> SendJsonGetAsync<TResponse>(
        string relativeUrl,
        string? deviceSecret,
        CancellationToken cancellationToken)
    {
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, relativeUrl);

            if (!string.IsNullOrWhiteSpace(deviceSecret))
            {
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceSecret);
            }

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            _healthTracker.RecordConnectionAttempt(true);

            return await HandleHttpResponseAsync<TResponse>(response, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("API request to {Url} cancelled by service shutdown.", relativeUrl);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _healthTracker.RecordConnectionAttempt(false);
            _logger.LogWarning("GET request to {Url} timed out: {Message}", relativeUrl, ex.Message);
            return ApiResponse<TResponse>.Failure("Connection timed out.", HttpStatusCode.RequestTimeout, isNetworkError: true);
        }
        catch (HttpRequestException ex)
        {
            _healthTracker.RecordConnectionAttempt(false);
            _logger.LogWarning("Network failure contacting {Url}: {Message}", relativeUrl, ex.Message);
            return ApiResponse<TResponse>.Failure($"Network error: {ex.Message}", ex.StatusCode ?? HttpStatusCode.ServiceUnavailable, isNetworkError: true);
        }
        catch (Exception ex)
        {
            _healthTracker.RecordConnectionAttempt(false);
            _logger.LogError(ex, "Unexpected error during GET {Url}", relativeUrl);
            return ApiResponse<TResponse>.Failure($"Unexpected error: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    private async Task<ApiResponse<TResponse>> HandleHttpResponseAsync<TResponse>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var rawContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            try
            {
                var data = JsonSerializer.Deserialize<TResponse>(rawContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (data == null)
                {
                    _logger.LogWarning("Received empty or malformed JSON from backend.");
                    return ApiResponse<TResponse>.Failure("Empty JSON response received from backend.", response.StatusCode);
                }

                return ApiResponse<TResponse>.Success(data, response.StatusCode);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize server response. Malformed JSON payload.");
                return ApiResponse<TResponse>.Failure("Malformed JSON response.", response.StatusCode);
            }
        }

        // Handle specific non-success status codes
        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
                _logger.LogWarning("Authentication failed (401 Unauthorized). Device token may be invalid or expired.");
                return ApiResponse<TResponse>.Failure("Unauthorized. Credential invalid.", HttpStatusCode.Unauthorized);

            case HttpStatusCode.Forbidden:
                _logger.LogWarning("Access forbidden (403 Forbidden). Device registration may be rejected or revoked.");
                return ApiResponse<TResponse>.Failure("Forbidden. Device enrollment rejected or revoked.", HttpStatusCode.Forbidden);

            case HttpStatusCode.NotFound:
                _logger.LogWarning("API endpoint or resource not found (404).");
                return ApiResponse<TResponse>.Failure("Resource not found.", HttpStatusCode.NotFound);

            case HttpStatusCode.InternalServerError:
            case HttpStatusCode.BadGateway:
            case HttpStatusCode.ServiceUnavailable:
            case HttpStatusCode.GatewayTimeout:
                _logger.LogWarning("Admin backend returned server error ({StatusCode}). Backend may be starting or restarting.", (int)response.StatusCode);
                return ApiResponse<TResponse>.Failure($"Backend server error: {response.StatusCode}", response.StatusCode, isNetworkError: true);

            default:
                _logger.LogWarning("Server returned non-success code: {StatusCode}", response.StatusCode);
                return ApiResponse<TResponse>.Failure($"HTTP {response.StatusCode}: {SecurityMasker.SanitizeLogMessage(rawContent)}", response.StatusCode);
        }
    }

    /// <summary>
    /// Configures custom certificate validation rules enforcing strict system trust or thumbprint pinning.
    /// Never allows dangerous "accept-all" callbacks.
    /// </summary>
    public static bool ValidateServerCertificate(
        HttpRequestMessage request,
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors,
        AgentOptions options)
    {
        if (certificate == null)
        {
            return false;
        }

        if (options.TlsValidationMode.Equals("PinnedThumbprint", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.PinnedCertificateThumbprint))
            {
                return false;
            }

            var certThumbprint = certificate.GetCertHashString(System.Security.Cryptography.HashAlgorithmName.SHA256);
            var cleanPinned = options.PinnedCertificateThumbprint.Replace(":", "").Replace(" ", "");
            return string.Equals(certThumbprint, cleanPinned, StringComparison.OrdinalIgnoreCase);
        }

        // Default: Strict standard system trust - require zero SSL policy errors
        return sslPolicyErrors == SslPolicyErrors.None;
    }
}
