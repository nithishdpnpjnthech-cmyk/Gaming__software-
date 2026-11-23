using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using GamingCafe.ClientAgent.Configuration;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Security;

[SupportedOSPlatform("windows")]
public sealed class DpapiCredentialProtection : ICredentialProtector
{
    private static readonly byte[] EntropySalt = Encoding.UTF8.GetBytes("GamingCafe-Agent-Entropy-V1-Salt-Key");
    private readonly DataProtectionScope _scope;
    private readonly ILogger<DpapiCredentialProtection> _logger;

    public DpapiCredentialProtection(IConfigurationService configService, ILogger<DpapiCredentialProtection> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var useMachineScope = configService.CurrentConfiguration.Security.UseMachineScope;
        _scope = useMachineScope ? DataProtectionScope.LocalMachine : DataProtectionScope.CurrentUser;
    }

    public byte[] Protect(byte[] unencryptedData)
    {
        ArgumentNullException.ThrowIfNull(unencryptedData);
        if (unencryptedData.Length == 0) return Array.Empty<byte>();

        try
        {
            return ProtectedData.Protect(unencryptedData, EntropySalt, _scope);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DPAPI encryption failed.");
            throw new CryptographicException("Failed to protect data via DPAPI.", ex);
        }
    }

    public byte[] Unprotect(byte[] encryptedData)
    {
        ArgumentNullException.ThrowIfNull(encryptedData);
        if (encryptedData.Length == 0) return Array.Empty<byte>();

        try
        {
            return ProtectedData.Unprotect(encryptedData, EntropySalt, _scope);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DPAPI decryption failed.");
            throw new CryptographicException("Failed to unprotect data via DPAPI.", ex);
        }
    }

    public string ProtectString(string unencrypted)
    {
        if (string.IsNullOrEmpty(unencrypted)) return string.Empty;
        var bytes = Encoding.UTF8.GetBytes(unencrypted);
        var protectedBytes = Protect(bytes);
        return Convert.ToBase64String(protectedBytes);
    }

    public string UnprotectString(string encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return string.Empty;
        var bytes = Convert.FromBase64String(encrypted);
        var unprotectedBytes = Unprotect(bytes);
        return Encoding.UTF8.GetString(unprotectedBytes);
    }
}
