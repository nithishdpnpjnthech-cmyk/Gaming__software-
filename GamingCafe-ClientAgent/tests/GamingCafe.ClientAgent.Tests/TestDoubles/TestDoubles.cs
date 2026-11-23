using System.Collections.Concurrent;
using System.Net;
using GamingCafe.ClientAgent.Authentication;
using GamingCafe.ClientAgent.Communication;
using GamingCafe.ClientAgent.Communication.ApiModels;
using GamingCafe.ClientAgent.Enrollment;
using GamingCafe.ClientAgent.Heartbeat;
using GamingCafe.ClientAgent.Identity;
using GamingCafe.ClientAgent.Realtime;

namespace GamingCafe.ClientAgent.Tests.TestDoubles;

public class TestDeviceIdentityStore : IDeviceIdentityStore
{
    private DeviceIdentity? _identity;
    public bool ExistsFlag = false;
    public bool CorruptStorageRecovered = false;

    public Task<DeviceIdentity?> LoadIdentityAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_identity);
    }

    public Task SaveIdentityAsync(DeviceIdentity identity, CancellationToken cancellationToken = default)
    {
        _identity = identity;
        ExistsFlag = true;
        return Task.CompletedTask;
    }

    public bool Exists() => ExistsFlag;

    public void RecoverCorruptStorage()
    {
        CorruptStorageRecovered = true;
        _identity = null;
        ExistsFlag = false;
    }

    public void SeedIdentity(DeviceIdentity identity)
    {
        _identity = identity;
        ExistsFlag = true;
    }
}

public class TestCredentialStore : IDeviceCredentialStore
{
    private readonly ConcurrentDictionary<string, string> _store = new(StringComparer.OrdinalIgnoreCase);

    public Task SaveCredentialAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        _store[key] = secret;
        return Task.CompletedTask;
    }

    public Task<string?> GetCredentialAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(key, out var val);
        return Task.FromResult(val);
    }

    public Task DeleteCredentialAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<bool> HasCredentialAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.ContainsKey(key));
    }
}

public class MockBackendApiClient : IBackendApiClient
{
    public Func<DeviceRegistrationRequest, ApiResponse<DeviceRegistrationResponse>>? OnRegister { get; set; }
    public Func<Guid, ApiResponse<DeviceEnrollmentStatusResponse>>? OnGetStatus { get; set; }
    public Func<HeartbeatPayload, string, ApiResponse<HeartbeatAck>>? OnSendHeartbeat { get; set; }

    public List<DeviceRegistrationRequest> CapturedRegistrations { get; } = new();
    public List<HeartbeatPayload> CapturedHeartbeats { get; } = new();

    public Task<ApiResponse<DeviceRegistrationResponse>> RegisterDeviceAsync(DeviceRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        CapturedRegistrations.Add(request);
        if (OnRegister != null) return Task.FromResult(OnRegister(request));

        return Task.FromResult(ApiResponse<DeviceRegistrationResponse>.Success(new DeviceRegistrationResponse
        {
            DeviceId = request.DeviceId,
            Status = "PendingApproval"
        }));
    }

    public Task<ApiResponse<DeviceEnrollmentStatusResponse>> GetEnrollmentStatusAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        if (OnGetStatus != null) return Task.FromResult(OnGetStatus(deviceId));

        return Task.FromResult(ApiResponse<DeviceEnrollmentStatusResponse>.Success(new DeviceEnrollmentStatusResponse
        {
            DeviceId = deviceId,
            Status = "PendingApproval"
        }));
    }

    public Task<ApiResponse<HeartbeatAck>> SendHeartbeatFallbackAsync(HeartbeatPayload payload, string deviceSecret, CancellationToken cancellationToken = default)
    {
        CapturedHeartbeats.Add(payload);
        if (OnSendHeartbeat != null) return Task.FromResult(OnSendHeartbeat(payload, deviceSecret));

        return Task.FromResult(ApiResponse<HeartbeatAck>.Success(new HeartbeatAck
        {
            Acknowledged = true,
            ServerTimestampUtc = DateTime.UtcNow,
            Status = "OK"
        }));
    }
}

public class MockSignalRHubClient : ISignalRHubClient
{
    public bool IsConnected { get; set; } = false;
    public bool SimulateSendFailure { get; set; } = false;
    public List<HeartbeatPayload> SentHeartbeats { get; } = new();

    public Task<bool> SendHeartbeatAsync(HeartbeatPayload payload, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || SimulateSendFailure)
        {
            return Task.FromResult(false);
        }

        SentHeartbeats.Add(payload);
        return Task.FromResult(true);
    }
}
