using GamingCafe.ClientAgent.Models;
using GamingCafe.ClientAgent.Models.Dto;

namespace GamingCafe.ClientAgent.Interfaces;

/// <summary>
/// Manages the generation, persistence, and safe recovery of the unique PC device identity.
/// </summary>
public interface IDeviceIdentityService
{
    /// <summary>
    /// Gets or initializes the device identity. Reuses existing identity if present,
    /// or generates and securely saves a new one. Recovers gracefully if corrupted.
    /// </summary>
    Task<DeviceIdentity> GetOrCreateIdentityAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a valid identity exists locally.
    /// </summary>
    bool HasIdentity();
}

/// <summary>
/// Secure credential storage abstraction using Windows DPAPI or test doubles.
/// Never logs credentials or exposes them via command-line / exceptions.
/// </summary>
public interface ICredentialStorage
{
    Task SaveCredentialAsync(string key, string secret, CancellationToken cancellationToken = default);
    Task<string?> GetCredentialAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteCredentialAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> HasCredentialAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>
/// Abstraction for local file persistence with atomic write safety and access control.
/// </summary>
public interface IFileStorage
{
    Task WriteTextAtomicAsync(string relativePath, string content, CancellationToken cancellationToken = default);
    Task<string?> ReadTextAsync(string relativePath, CancellationToken cancellationToken = default);
    Task WriteBytesAtomicAsync(string relativePath, byte[] data, CancellationToken cancellationToken = default);
    Task<byte[]?> ReadBytesAsync(string relativePath, CancellationToken cancellationToken = default);
    bool Exists(string relativePath);
    void Delete(string relativePath);
    string GetFullPath(string relativePath);
}

/// <summary>
/// HTTP communication contract with the Admin backend.
/// </summary>
public interface IApiClient
{
    Task<ApiResponse<DeviceRegistrationResponse>> RegisterDeviceAsync(DeviceRegistrationRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<DeviceEnrollmentStatusResponse>> CheckEnrollmentStatusAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task<ApiResponse<HeartbeatResponse>> SendHeartbeatAsync(HeartbeatRequest request, string deviceSecret, CancellationToken cancellationToken = default);
}

/// <summary>
/// Orchestrates the enrollment lifecycle from Unregistered to Authenticated.
/// </summary>
public interface IEnrollmentService
{
    EnrollmentState CurrentState { get; }
    Task<EnrollmentState> EvaluateInitialStateAsync(DeviceIdentity identity, CancellationToken cancellationToken = default);
    Task<EnrollmentState> ProcessEnrollmentStepAsync(DeviceIdentity identity, CancellationToken cancellationToken = default);
    Task HandleRevocationAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Periodically reports presence and basic health to the Admin backend.
/// </summary>
public interface IHeartbeatService
{
    Task<bool> SendHeartbeatOnceAsync(DeviceIdentity identity, CancellationToken cancellationToken = default);
}

/// <summary>
/// Tracks privacy-respecting client operational metrics.
/// </summary>
public interface IHealthTracker
{
    void RecordServiceStarted();
    void RecordConnectionAttempt(bool success);
    void RecordHeartbeatSent(bool success);
    void RecordStateChange(EnrollmentState state);
    HealthMetrics GetCurrentMetrics();
}

/// <summary>
/// Exponential backoff with jitter calculator for retry policies.
/// </summary>
public interface IRetryPolicy
{
    TimeSpan GetNextDelay(int attemptCount);
    void Reset();
}
