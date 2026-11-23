using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Identity;

/// <summary>
/// Manages device identity lifecycle, cryptographic UUIDv4 generation, and restart reuse.
/// </summary>
public class DeviceIdentityService : IDeviceIdentityService
{
    private readonly IDeviceIdentityStore _store;
    private readonly ILogger<DeviceIdentityService> _logger;
    private DeviceIdentity? _cachedIdentity;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public DeviceIdentityService(IDeviceIdentityStore store, ILogger<DeviceIdentityService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public bool HasIdentity() => _cachedIdentity != null || _store.Exists();

    public async Task<DeviceIdentity> GetOrCreateIdentityAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedIdentity != null)
        {
            return _cachedIdentity;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedIdentity != null)
            {
                return _cachedIdentity;
            }

            if (_store.Exists())
            {
                var existing = await _store.LoadIdentityAsync(cancellationToken);
                if (existing != null)
                {
                    _cachedIdentity = existing;
                    _logger.LogInformation("Loaded persistent Device ID: {DeviceId} for Host: {Hostname}",
                        existing.DeviceId, existing.Hostname);
                    return existing;
                }

                _logger.LogWarning("Existing identity was invalid or corrupted. Recovering storage and generating new identity.");
                _store.RecoverCorruptStorage();
            }

            var newlyGenerated = GenerateCryptographicIdentity();
            await _store.SaveIdentityAsync(newlyGenerated, cancellationToken);
            _cachedIdentity = newlyGenerated;

            _logger.LogInformation("Generated and persisted new Device ID: {DeviceId} for Host: {Hostname}",
                newlyGenerated.DeviceId, newlyGenerated.Hostname);

            return newlyGenerated;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static DeviceIdentity GenerateCryptographicIdentity()
    {
        // Cryptographically strong UUIDv4 generation
        byte[] bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);

        // Set version 4 (bits 12-15 = 0100)
        bytes[7] = (byte)((bytes[7] & 0x0f) | 0x40);
        // Set variant RFC 4122 (bits 6-7 = 10)
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);

        var deviceId = new Guid(bytes);
        var hostname = Environment.MachineName;
        var os = RuntimeInformation.OSDescription;

        // Calculate non-authoritative hardware digest for clone detection
        var rawData = $"{hostname}_{Environment.ProcessorCount}_{Environment.OSVersion}";
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawData));
        var digest = Convert.ToHexString(hash);

        return new DeviceIdentity
        {
            DeviceId = deviceId,
            Hostname = hostname,
            OperatingSystem = os,
            CreatedAtUtc = DateTime.UtcNow,
            HardwareDigest = digest
        };
    }
}
