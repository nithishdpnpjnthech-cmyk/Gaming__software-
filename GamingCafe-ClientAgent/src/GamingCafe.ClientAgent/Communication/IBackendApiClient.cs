using System.Text.Json.Serialization;

namespace GamingCafe.ClientAgent.Communication;

public class DeviceRegistrationRequestDto
{
    [JsonPropertyName("deviceCode")]
    public string DeviceCode { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("hostname")]
    public string? Hostname { get; set; }

    [JsonPropertyName("hardwareFingerprintHash")]
    public string? HardwareFingerprintHash { get; set; }

    [JsonPropertyName("agentVersion")]
    public string? AgentVersion { get; set; }
}

public class DeviceRegistrationResponseDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("deviceCode")]
    public string DeviceCode { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("registrationStatus")]
    public string RegistrationStatus { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public enum RegistrationResultStatus
{
    Success,         // 201 Created
    AlreadyExists,   // 409 Conflict
    ValidationError, // 400 Bad Request
    NetworkFailure,  // Sockets, DNS, TLS, Timeout
    ServerError,     // 5xx
    UnexpectedResponse
}

public class DeviceRegistrationResult
{
    public RegistrationResultStatus Status { get; set; }
    public DeviceRegistrationResponseDto? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public int? StatusCode { get; set; }
}

public enum DeviceVerificationStatus
{
    Confirmed,       // Device exists on backend
    NotFound,        // HTTP 404 - Device does not exist on backend (stale/deleted/db wiped)
    Unauthorized,    // HTTP 401 / 403 - Auth policy requires token
    NetworkFailure,  // Connection refused / timeout / DNS failure
    ServerError      // 5xx or unexpected error
}

public class DeviceVerificationResult
{
    public DeviceVerificationStatus Status { get; set; }
    public int? StatusCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface IBackendApiClient
{
    Task<DeviceRegistrationResult> RegisterDeviceAsync(
        DeviceRegistrationRequestDto request, 
        CancellationToken cancellationToken = default);

    Task<DeviceVerificationResult> VerifyDeviceExistsAsync(
        Guid backendDeviceId,
        CancellationToken cancellationToken = default);
}
