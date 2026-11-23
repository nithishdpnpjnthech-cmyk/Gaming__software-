using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Security;

/// <summary>
/// Credential protection contract for encrypting sensitive tokens at rest.
/// </summary>
public interface ICredentialProtection
{
    byte[] Protect(byte[] plainData);
    byte[] Unprotect(byte[] cipherData);
}

/// <summary>
/// Hardware/OS-backed credential protection using Windows Data Protection API (DPAPI).
/// Utilizes application-level entropy to prevent ciphertext reuse across different services.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class DpapiCredentialProtection : ICredentialProtection
{
    private readonly DataProtectionScope _scope;
    private readonly byte[] _entropy;
    private readonly ILogger<DpapiCredentialProtection> _logger;

    public DpapiCredentialProtection(
        ILogger<DpapiCredentialProtection> logger,
        DataProtectionScope scope = DataProtectionScope.CurrentUser)
    {
        _logger = logger;
        _scope = scope;
        _entropy = Encoding.UTF8.GetBytes("GamingCafe-Agent-v1-ContextEntropy");
    }

    public byte[] Protect(byte[] plainData)
    {
        ArgumentNullException.ThrowIfNull(plainData);
        try
        {
            return ProtectedData.Protect(plainData, _entropy, _scope);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "DPAPI encryption failed.");
            throw new InvalidOperationException("Failed to encrypt sensitive data via Windows DPAPI.", ex);
        }
    }

    public byte[] Unprotect(byte[] cipherData)
    {
        ArgumentNullException.ThrowIfNull(cipherData);
        try
        {
            return ProtectedData.Unprotect(cipherData, _entropy, _scope);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "DPAPI decryption failed. Key scope mismatch or data corruption.");
            throw new InvalidOperationException("Failed to decrypt sensitive data via Windows DPAPI.", ex);
        }
    }
}

/// <summary>
/// Fallback/in-memory protection for non-Windows platforms and unit tests.
/// </summary>
public class PlainTextTestCredentialProtection : ICredentialProtection
{
    public byte[] Protect(byte[] plainData) => (byte[])plainData.Clone();
    public byte[] Unprotect(byte[] cipherData) => (byte[])cipherData.Clone();
}
