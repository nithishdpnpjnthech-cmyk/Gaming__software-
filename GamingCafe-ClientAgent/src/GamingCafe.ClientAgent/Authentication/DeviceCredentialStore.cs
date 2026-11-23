using System.Runtime.InteropServices;
using System.Text;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GamingCafe.ClientAgent.Authentication;

/// <summary>
/// Abstraction contract for encrypted local credential storage.
/// </summary>
public interface IDeviceCredentialStore
{
    Task SaveCredentialAsync(string key, string secret, CancellationToken cancellationToken = default);
    Task<string?> GetCredentialAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteCredentialAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> HasCredentialAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>
/// Production credential store persisting DPAPI-encrypted secrets to protected local disk.
/// </summary>
public class DeviceCredentialStore : IDeviceCredentialStore
{
    private readonly string _storageDirectory;
    private readonly ICredentialProtection _protection;
    private readonly ILogger<DeviceCredentialStore> _logger;

    public DeviceCredentialStore(
        IOptions<AgentConfiguration> options,
        ICredentialProtection protection,
        ILogger<DeviceCredentialStore> logger)
    {
        _protection = protection;
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
        SecurityConfiguration.ApplyRestrictiveDirectoryAcl(_storageDirectory, _logger);
    }

    private string GetFilePath(string key)
    {
        var sanitizedKey = string.Concat(key.Where(char.IsLetterOrDigit));
        return Path.Combine(_storageDirectory, $"cred_{sanitizedKey}.dat");
    }

    public async Task SaveCredentialAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        var filePath = GetFilePath(key);
        var plainBytes = Encoding.UTF8.GetBytes(secret);
        var cipherBytes = _protection.Protect(plainBytes);

        var tempPath = filePath + $".{Guid.NewGuid():N}.tmp";
        await File.WriteAllBytesAsync(tempPath, cipherBytes, cancellationToken);

        if (File.Exists(filePath))
        {
            File.Replace(tempPath, filePath, null);
        }
        else
        {
            File.Move(tempPath, filePath);
        }

        _logger.LogInformation("Credential securely encrypted and persisted for key: {Key}", key);
    }

    public async Task<string?> GetCredentialAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var filePath = GetFilePath(key);

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var cipherBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            if (cipherBytes.Length == 0) return null;

            var plainBytes = _protection.Unprotect(cipherBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt credential {Key}.", key);
            return null;
        }
    }

    public Task DeleteCredentialAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var filePath = GetFilePath(key);

        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted credential: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete credential: {Key}", key);
            }
        }

        return Task.CompletedTask;
    }

    public Task<bool> HasCredentialAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Task.FromResult(File.Exists(GetFilePath(key)));
    }
}
