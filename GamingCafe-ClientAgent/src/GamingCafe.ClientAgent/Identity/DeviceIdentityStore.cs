using System.Text.Json;
using GamingCafe.ClientAgent.Configuration;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Identity;

public sealed class DeviceIdentityStore : IDeviceIdentityStore
{
    private readonly string _filePath;
    private readonly string _storageDir;
    private readonly ILogger<DeviceIdentityStore> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public DeviceIdentityStore(IConfigurationService configService, ILogger<DeviceIdentityStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var rawDir = configService.CurrentConfiguration.Identity.StorageDirectory;
        _storageDir = StoragePathResolver.ResolveStorageDirectory(rawDir, _logger);
        _filePath = Path.Combine(_storageDir, "device-identity.json");

        _logger.LogInformation(
            "DeviceIdentityStore initialized. BaseDirectory: '{BaseDir}', CurrentDirectory: '{CurrentDir}', ConfiguredStorageDir: '{ConfiguredDir}', ResolvedStorageDir: '{ResolvedDir}', FilePath: '{FilePath}', FileExists: {Exists}",
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
            rawDir,
            _storageDir,
            _filePath,
            File.Exists(_filePath));
    }

    public string StorageDirectory => _storageDir;
    public string IdentityFilePath => _filePath;

    public bool IdentityExists() => File.Exists(_filePath);

    public DeviceIdentity? LoadIdentity()
    {
        _logger.LogInformation("Loading device identity from resolved path: '{FilePath}' (Exists: {Exists})",
            _filePath, File.Exists(_filePath));

        if (!File.Exists(_filePath))
        {
            _logger.LogWarning("No device identity file found at '{FilePath}'. A new persistent identity will be generated.", _filePath);
            return null;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var identity = JsonSerializer.Deserialize<DeviceIdentity>(json, JsonOptions);
            if (identity == null || identity.DeviceId == Guid.Empty)
            {
                QuarantineCorruptedFile("Identity object was null or contained empty GUID.");
                return null;
            }

            _logger.LogInformation(
                "Successfully loaded identity from '{FilePath}'. DeviceId: {DeviceId}, BackendDeviceId: {BackendId}, DeviceCode: '{Code}', Status: '{Status}'",
                _filePath, identity.DeviceId, identity.BackendDeviceId, identity.DeviceCode, identity.RegistrationStatus);

            return identity;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Device identity file at '{FilePath}' is malformed JSON.", _filePath);
            QuarantineCorruptedFile(ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error reading device identity from '{FilePath}'.", _filePath);
            throw;
        }
    }

    public void SaveIdentity(DeviceIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        if (identity.DeviceId == Guid.Empty)
        {
            throw new ArgumentException("DeviceId cannot be empty Guid.", nameof(identity));
        }

        Directory.CreateDirectory(_storageDir);

        var tempFilePath = _filePath + $".tmp.{Guid.NewGuid():N}";
        var json = JsonSerializer.Serialize(identity, JsonOptions);

        try
        {
            File.WriteAllText(tempFilePath, json);
            File.Move(tempFilePath, _filePath, overwrite: true);
            _logger.LogInformation("Saved device identity for DeviceId '{DeviceId}'.", identity.DeviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist device identity to '{FilePath}'.", _filePath);
            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { /* ignore */ }
            }
            throw;
        }
    }

    public void DeleteIdentity()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
            _logger.LogWarning("Device identity file deleted at '{FilePath}'.", _filePath);
        }
    }

    private void QuarantineCorruptedFile(string reason)
    {
        try
        {
            var quarantinePath = _filePath + $".corrupted.{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            if (File.Exists(_filePath))
            {
                File.Move(_filePath, quarantinePath, overwrite: true);
                _logger.LogCritical("Corrupted identity quarantined to '{QuarantinePath}'. Reason: {Reason}", quarantinePath, reason);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to quarantine corrupted identity file '{FilePath}'.", _filePath);
        }
    }
}
