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

public interface IBackendApiClient
{
    Task<DeviceRegistrationResult> RegisterDeviceAsync(
        DeviceRegistrationRequestDto request, 
        CancellationToken cancellationToken = default);
}
