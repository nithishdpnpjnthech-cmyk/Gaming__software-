using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using GamingCafe.ClientAgent.Communication.ApiModels;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Enrollment;
using GamingCafe.ClientAgent.Heartbeat;
using GamingCafe.ClientAgent.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GamingCafe.ClientAgent.Communication;

/// <summary>
/// Contract for outbound REST API communication with the Admin PC backend.
/// </summary>
public interface IBackendApiClient
{
    Task<ApiResponse<DeviceRegistrationResponse>> RegisterDeviceAsync(DeviceRegistrationRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<DeviceEnrollmentStatusResponse>> GetEnrollmentStatusAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task<ApiResponse<HeartbeatAck>> SendHeartbeatFallbackAsync(HeartbeatPayload payload, string deviceSecret, CancellationToken cancellationToken = default);
}

/// <summary>
/// Managed HTTPS REST client using IHttpClientFactory with strict TLS and resilient error mapping.
/// </summary>
public class BackendApiClient : IBackendApiClient
{
    public const string HttpClientName = "BackendApiClient";

    // Placeholders for backend API routes (pending final backend alignment)
    public const string RouteRegister = "/api/v1/devices/register";
    public const string RouteEnrollmentStatus = "/api/v1/devices/{0}/status";
    public const string RouteHeartbeat = "/api/v1/devices/heartbeat";

    private readonly HttpClient _httpClient;
    private readonly IOptions<AgentConfiguration> _options;
    private readonly ILogger<BackendApiClient> _logger;

    public BackendApiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<AgentConfiguration> options,
        ILogger<BackendApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient(HttpClientName);
        _options = options;
        _logger = logger;
    }

    public async Task<ApiResponse<DeviceRegistrationResponse>> RegisterDeviceAsync(
        DeviceRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending REST registration request for DeviceId: {DeviceId}, Hostname: {Hostname}",
            request.DeviceId, request.Hostname);

        return await PostJsonAsync<DeviceRegistrationRequest, DeviceRegistrationResponse>(
            RouteRegister,
            request,
            deviceSecret: null,
            cancellationToken);
    }

    public async Task<ApiResponse<DeviceEnrollmentStatusResponse>> GetEnrollmentStatusAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var route = string.Format(RouteEnrollmentStatus, deviceId);
        _logger.LogDebug("Polling enrollment status via REST at: {Route}", route);

        return await GetJsonAsync<DeviceEnrollmentStatusResponse>(route, deviceSecret: null, cancellationToken);
    }

    public async Task<ApiResponse<HeartbeatAck>> SendHeartbeatFallbackAsync(
        HeartbeatPayload payload,
        string deviceSecret,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Sending fallback REST heartbeat for DeviceId: {DeviceId}", payload.DeviceId);

        return await PostJsonAsync<HeartbeatPayload, HeartbeatAck>(
            RouteHeartbeat,
            payload,
            deviceSecret,
            cancellationToken);
    }

    private async Task<ApiResponse<TResponse>> PostJsonAsync<TRequest, TResponse>(
        string relativeUrl,
        TRequest requestBody,
        string? deviceSecret,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, relativeUrl);
            var json = JsonSerializer.Serialize(requestBody);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            if (!string.IsNullOrWhiteSpace(deviceSecret))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceSecret);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return await ParseHttpResponseAsync<TResponse>(response, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("REST request to {Url} cancelled by service shutdown.", relativeUrl);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning("REST request to {Url} timed out: {Message}", relativeUrl, ex.Message);
            return ApiResponse<TResponse>.Failure("Connection timed out.", HttpStatusCode.RequestTimeout, isNetworkError: true);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("Network failure contacting {Url}: {Message}", relativeUrl, ex.Message);
            return ApiResponse<TResponse>.Failure($"Network error: {ex.Message}", ex.StatusCode ?? HttpStatusCode.ServiceUnavailable, isNetworkError: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected exception during REST call to {Url}", relativeUrl);
            return ApiResponse<TResponse>.Failure($"Unexpected error: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    private async Task<ApiResponse<TResponse>> GetJsonAsync<TResponse>(
        string relativeUrl,
        string? deviceSecret,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);

            if (!string.IsNullOrWhiteSpace(deviceSecret))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceSecret);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return await ParseHttpResponseAsync<TResponse>(response, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("REST GET to {Url} cancelled by service shutdown.", relativeUrl);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning("REST GET to {Url} timed out: {Message}", relativeUrl, ex.Message);
            return ApiResponse<TResponse>.Failure("Connection timed out.", HttpStatusCode.RequestTimeout, isNetworkError: true);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("Network failure during GET {Url}: {Message}", relativeUrl, ex.Message);
            return ApiResponse<TResponse>.Failure($"Network error: {ex.Message}", ex.StatusCode ?? HttpStatusCode.ServiceUnavailable, isNetworkError: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during GET {Url}", relativeUrl);
            return ApiResponse<TResponse>.Failure($"Unexpected error: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    private async Task<ApiResponse<TResponse>> ParseHttpResponseAsync<TResponse>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            try
            {
                var data = JsonSerializer.Deserialize<TResponse>(raw, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (data == null)
                {
                    return ApiResponse<TResponse>.Failure("Empty JSON response received.", response.StatusCode);
                }

                return ApiResponse<TResponse>.Success(data, response.StatusCode);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize server response payload.");
                return ApiResponse<TResponse>.Failure("Malformed JSON response.", response.StatusCode);
            }
        }

        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
                _logger.LogWarning("REST Authentication failed (401 Unauthorized). Device token may be invalid.");
                return ApiResponse<TResponse>.Failure("Unauthorized.", HttpStatusCode.Unauthorized);

            case HttpStatusCode.Forbidden:
                _logger.LogWarning("REST Forbidden (403 Forbidden). Device enrollment may be rejected or revoked.");
                return ApiResponse<TResponse>.Failure("Forbidden.", HttpStatusCode.Forbidden);

            case HttpStatusCode.NotFound:
                return ApiResponse<TResponse>.Failure("Resource not found.", HttpStatusCode.NotFound);

            case HttpStatusCode.InternalServerError:
            case HttpStatusCode.BadGateway:
            case HttpStatusCode.ServiceUnavailable:
            case HttpStatusCode.GatewayTimeout:
                return ApiResponse<TResponse>.Failure($"Server error: {response.StatusCode}", response.StatusCode, isNetworkError: true);

            default:
                return ApiResponse<TResponse>.Failure($"HTTP {response.StatusCode}: {SecurityMasker.SanitizeLogMessage(raw)}", response.StatusCode);
        }
    }

    public static bool ValidateServerCertificate(
        HttpRequestMessage request,
        X509Certificate2? cert,
        X509Chain? chain,
        SslPolicyErrors errors,
        AgentConfiguration options)
    {
        if (cert == null) return false;

        if (options.TlsValidationMode.Equals("PinnedThumbprint", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.PinnedCertificateThumbprint)) return false;
            var cleanPinned = options.PinnedCertificateThumbprint.Replace(":", "").Replace(" ", "");
            var thumbprint = cert.GetCertHashString(System.Security.Cryptography.HashAlgorithmName.SHA256);
            return string.Equals(thumbprint, cleanPinned, StringComparison.OrdinalIgnoreCase);
        }

        // Default: Strict system CA trust
        return errors == SslPolicyErrors.None;
    }
}
