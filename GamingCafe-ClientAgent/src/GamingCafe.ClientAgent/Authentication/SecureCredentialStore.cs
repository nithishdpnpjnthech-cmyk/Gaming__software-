using System.Text.Json;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Security;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Authentication;

public sealed class SecureCredentialStore : ISecureCredentialStore
{
    private readonly string _filePath;
    private readonly string _storageDir;
    private readonly ICredentialProtector _protector;
    private readonly ILogger<SecureCredentialStore> _logger;

    public SecureCredentialStore(
        IConfigurationService configService,
        ICredentialProtector protector,
        ILogger<SecureCredentialStore> logger)
    {
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storageDir = Path.GetFullPath(configService.CurrentConfiguration.Identity.StorageDirectory);
        _filePath = Path.Combine(_storageDir, "device-credentials.dat");
    }

    public bool HasCredentials() => File.Exists(_filePath);

    public DeviceCredentials? LoadCredentials()
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        try
        {
            var encryptedBytes = File.ReadAllBytes(_filePath);
            if (encryptedBytes.Length == 0) return null;

            var decryptedBytes = _protector.Unprotect(encryptedBytes);
            var json = System.Text.Encoding.UTF8.GetString(decryptedBytes);
            var creds = JsonSerializer.Deserialize<DeviceCredentials>(json);
            return creds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt and load credentials from '{FilePath}'.", _filePath);
            return null;
        }
    }

    public void SaveCredentials(DeviceCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        Directory.CreateDirectory(_storageDir);

        var json = JsonSerializer.Serialize(credentials);
        var jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
        var encryptedBytes = _protector.Protect(jsonBytes);

        var tempFilePath = _filePath + $".tmp.{Guid.NewGuid():N}";
        try
        {
            File.WriteAllBytes(tempFilePath, encryptedBytes);
            File.Move(tempFilePath, _filePath, overwrite: true);
            _logger.LogInformation("Securely persisted credentials for device '{DeviceId}'.", credentials.DeviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist encrypted credentials to '{FilePath}'.", _filePath);
            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { /* ignore */ }
            }
            throw;
        }
    }

    public void ClearCredentials()
    {
        if (File.Exists(_filePath))
        {
            try
            {
                File.Delete(_filePath);
                _logger.LogWarning("Cleared stored credentials from '{FilePath}'.", _filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete credentials file at '{FilePath}'.", _filePath);
            }
        }
    }
}
