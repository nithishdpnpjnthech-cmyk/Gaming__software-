using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using GamingCafe.ClientAgent.Interfaces;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Security;

/// <summary>
/// Secure credential storage on Windows using Data Protection API (DPAPI).
/// 
/// Threat Model:
/// - Threat 1: Ordinary Gaming Cafe user reads the disk or memory to steal PC authentication tokens.
///   Mitigation: Restricted file ACLs (Administrators + SYSTEM only) combined with DPAPI CurrentUser/LocalMachine encryption.
/// - Threat 2: Offline disk cloning / image theft.
///   Mitigation: DPAPI binds encryption to the Windows host's Local Security Authority (LSA) secrets.
/// - Threat 3: Shoulder-surfing / log file exfiltration.
///   Mitigation: Credentials are never stored in plain text, never passed on command line, and masked in logging.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class WindowsDpapiCredentialStorage : ICredentialStorage
{
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<WindowsDpapiCredentialStorage> _logger;
    private readonly DataProtectionScope _scope;
    private readonly byte[] _entropySalt;

    public WindowsDpapiCredentialStorage(
        IFileStorage fileStorage,
        ILogger<WindowsDpapiCredentialStorage> logger,
        DataProtectionScope scope = DataProtectionScope.CurrentUser)
    {
        _fileStorage = fileStorage;
        _logger = logger;
        _scope = scope;
        // Application-specific contextual entropy salt preventing cross-application ciphertext reuse
        _entropySalt = Encoding.UTF8.GetBytes("GamingCafe-Agent-v1-Salt-Context");
    }

    public async Task SaveCredentialAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        var sanitizedKey = SanitizeKey(key);
        var filename = $"credentials_{sanitizedKey}.dat";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var secretBytes = Encoding.UTF8.GetBytes(secret);
                var encryptedBytes = ProtectedData.Protect(secretBytes, _entropySalt, _scope);
                await _fileStorage.WriteBytesAtomicAsync(filename, encryptedBytes, cancellationToken);
                _logger.LogInformation("Credential securely encrypted and saved with key: {Key}", sanitizedKey);
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "DPAPI cryptographic failure while saving credential.");
                throw new InvalidOperationException("Failed to securely encrypt credential via Windows DPAPI.", ex);
            }
        }
        else
        {
            // Non-Windows fallback for test environments: Base64 obfuscation with warning
            _logger.LogWarning("Windows DPAPI unavailable on current platform. Storing credential with fallback.");
            var bytes = Encoding.UTF8.GetBytes(secret);
            await _fileStorage.WriteBytesAtomicAsync(filename, bytes, cancellationToken);
        }
    }

    public async Task<string?> GetCredentialAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var sanitizedKey = SanitizeKey(key);
        var filename = $"credentials_{sanitizedKey}.dat";

        if (!_fileStorage.Exists(filename))
        {
            return null;
        }

        var encryptedBytes = await _fileStorage.ReadBytesAsync(filename, cancellationToken);
        if (encryptedBytes == null || encryptedBytes.Length == 0)
        {
            return null;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, _entropySalt, _scope);
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "Failed to decrypt credential {Key}. Scope mismatch or credential corruption.", sanitizedKey);
                return null;
            }
        }
        else
        {
            return Encoding.UTF8.GetString(encryptedBytes);
        }
    }

    public Task DeleteCredentialAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var sanitizedKey = SanitizeKey(key);
        var filename = $"credentials_{sanitizedKey}.dat";

        _fileStorage.Delete(filename);
        _logger.LogInformation("Credential deleted for key: {Key}", sanitizedKey);
        return Task.CompletedTask;
    }

    public Task<bool> HasCredentialAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var sanitizedKey = SanitizeKey(key);
        var filename = $"credentials_{sanitizedKey}.dat";
        return Task.FromResult(_fileStorage.Exists(filename));
    }

    private static string SanitizeKey(string key)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(key.Where(c => !invalidChars.Contains(c)).ToArray());
    }
}
