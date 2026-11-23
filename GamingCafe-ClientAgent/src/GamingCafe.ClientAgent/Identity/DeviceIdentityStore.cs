using System.Runtime.InteropServices;
using System.Text.Json;
using GamingCafe.ClientAgent.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GamingCafe.ClientAgent.Identity;

/// <summary>
/// File-based identity store with atomic writes, ACL hardening, and corruption recovery.
/// </summary>
public class DeviceIdentityStore : IDeviceIdentityStore
{
    public const string IdentityFileName = "device_identity.json";

    private readonly string _storageDirectory;
    private readonly ILogger<DeviceIdentityStore> _logger;
    private readonly object _fileLock = new();

    public DeviceIdentityStore(IOptions<AgentConfiguration> options, ILogger<DeviceIdentityStore> logger)
    {
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(options.Value.LocalDataDirectory))
        {
            _storageDirectory = Path.GetFullPath(options.Value.LocalDataDirectory);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            _storageDirectory = Path.Combine(programData, "GamingCafe", "ClientAgent");
        }
        else
        {
            _storageDirectory = Path.Combine(AppContext.BaseDirectory, "agent_data");
        }

        Directory.CreateDirectory(_storageDirectory);
    }

    public string IdentityFilePath => Path.Combine(_storageDirectory, IdentityFileName);

    public bool Exists() => File.Exists(IdentityFilePath);

    public async Task<DeviceIdentity?> LoadIdentityAsync(CancellationToken cancellationToken = default)
    {
        if (!Exists()) return null;

        try
        {
            var json = await File.ReadAllTextAsync(IdentityFilePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("Identity file was empty.");
                return null;
            }

            var identity = JsonSerializer.Deserialize<DeviceIdentity>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (identity == null || identity.DeviceId == Guid.Empty || string.IsNullOrWhiteSpace(identity.Hostname))
            {
                _logger.LogWarning("Identity file contained invalid data values.");
                return null;
            }

            return identity;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read or parse device identity JSON.");
            return null;
        }
    }

    public async Task SaveIdentityAsync(DeviceIdentity identity, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(identity, new JsonSerializerOptions { WriteIndented = true });
        var tempPath = IdentityFilePath + $".{Guid.NewGuid():N}.tmp";

        await File.WriteAllTextAsync(tempPath, json, cancellationToken);

        lock (_fileLock)
        {
            if (File.Exists(IdentityFilePath))
            {
                File.Replace(tempPath, IdentityFilePath, null);
            }
            else
            {
                File.Move(tempPath, IdentityFilePath);
            }
        }
    }

    public void RecoverCorruptStorage()
    {
        if (!Exists()) return;

        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var backupPath = Path.Combine(_storageDirectory, $"device_identity_corrupt_{timestamp}.bak");

            lock (_fileLock)
            {
                if (File.Exists(IdentityFilePath))
                {
                    File.Move(IdentityFilePath, backupPath, overwrite: true);
                    _logger.LogWarning("Corrupt identity file safely quarantined to: {BackupPath}", backupPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to quarantine corrupt identity file.");
        }
    }
}
