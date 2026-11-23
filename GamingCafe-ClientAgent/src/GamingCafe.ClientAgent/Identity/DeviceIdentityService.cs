using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using GamingCafe.ClientAgent.Configuration;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Identity;

public sealed class DeviceIdentityService : IDeviceIdentityService
{
    private readonly IDeviceIdentityStore _store;
    private readonly IConfigurationService _configService;
    private readonly ILogger<DeviceIdentityService> _logger;
    private DeviceIdentity? _cachedIdentity;
    private readonly object _lock = new();

    public DeviceIdentityService(
        IDeviceIdentityStore store,
        IConfigurationService configService,
        ILogger<DeviceIdentityService> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public DeviceIdentity CurrentIdentity
    {
        get
        {
            if (_cachedIdentity == null)
            {
                lock (_lock)
                {
                    _cachedIdentity ??= GetOrCreateIdentity();
                }
            }
            return _cachedIdentity;
        }
    }

    public DeviceIdentity GetOrCreateIdentity()
    {
        lock (_lock)
        {
            var existing = _store.LoadIdentity();
            if (existing != null)
            {
                _logger.LogInformation("Loaded existing device identity: {DeviceId} ({DeviceName})", existing.DeviceId, existing.DeviceName);
                
                // Refresh hardware/environmental properties if changed
                var currentMac = GetPrimaryMacAddress();
                var currentName = Environment.MachineName;
                var currentOs = RuntimeInformation.OSDescription;
                var clientVer = _configService.CurrentConfiguration.Identity.ClientVersion;

                if (existing.DeviceName != currentName ||
                    existing.MacAddress != currentMac ||
                    existing.OsVersion != currentOs ||
                    existing.ClientVersion != clientVer)
                {
                    existing.DeviceName = currentName;
                    existing.MacAddress = currentMac;
                    existing.OsVersion = currentOs;
                    existing.ClientVersion = clientVer;
                    existing.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
                    _store.SaveIdentity(existing);
                    _logger.LogInformation("Updated device environmental attributes for {DeviceId}", existing.DeviceId);
                }

                _cachedIdentity = existing;
                return existing;
            }

            // Create new persistent identity
            var newId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var identity = new DeviceIdentity
            {
                DeviceId = newId,
                DeviceName = Environment.MachineName,
                HardwareIdentifier = GenerateHardwareFingerprint(),
                MacAddress = GetPrimaryMacAddress(),
                OsVersion = RuntimeInformation.OSDescription,
                ClientVersion = _configService.CurrentConfiguration.Identity.ClientVersion,
                FirstEnrolledAtUtc = now,
                LastUpdatedAtUtc = now
            };

            _store.SaveIdentity(identity);
            _logger.LogInformation("Generated new device identity: {DeviceId} ({DeviceName})", identity.DeviceId, identity.DeviceName);
            _cachedIdentity = identity;
            return identity;
        }
    }

    public void RefreshIdentity()
    {
        lock (_lock)
        {
            _cachedIdentity = null;
            _ = CurrentIdentity;
        }
    }

    private static string GetPrimaryMacAddress()
    {
        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .OrderByDescending(n => n.Speed)
                .FirstOrDefault();

            if (nic != null)
            {
                var address = nic.GetPhysicalAddress().ToString();
                if (!string.IsNullOrEmpty(address))
                {
                    return string.Join(":", Enumerable.Range(0, address.Length / 2)
                        .Select(i => address.Substring(i * 2, 2)));
                }
            }
        }
        catch
        {
            // Fallback
        }

        return "00:00:00:00:00:00";
    }

    private static string GenerateHardwareFingerprint()
    {
        try
        {
            var raw = $"{Environment.MachineName}|{Environment.ProcessorCount}|{RuntimeInformation.OSArchitecture}|{RuntimeInformation.OSDescription}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
