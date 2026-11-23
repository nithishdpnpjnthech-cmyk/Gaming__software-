using System.Runtime.InteropServices;
using System.Security.Cryptography;
using GamingCafe.ClientAgent.Authentication;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Security;
using GamingCafe.ClientAgent.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GamingCafe.ClientAgent.Tests.CredentialStorage;

public class CredentialStorageAndMaskingTests
{
    [Fact]
    public async Task TestCredentialStore_Lifecycle_SavesRetrievesAndDeletesCorrectly()
    {
        var store = new TestCredentialStore();
        const string key = "test_key";
        const string secret = "super_secret_token_12345";

        Assert.False(await store.HasCredentialAsync(key));
        Assert.Null(await store.GetCredentialAsync(key));

        await store.SaveCredentialAsync(key, secret);
        Assert.True(await store.HasCredentialAsync(key));
        Assert.Equal(secret, await store.GetCredentialAsync(key));

        await store.DeleteCredentialAsync(key);
        Assert.False(await store.HasCredentialAsync(key));
        Assert.Null(await store.GetCredentialAsync(key));
    }

    [Fact]
    public void SecurityMasker_MaskSecret_ObfuscatesSecretSafely()
    {
        var shortSecret = "12345";
        var maskedShort = SecurityMasker.MaskSecret(shortSecret);
        Assert.Equal("******", maskedShort);

        var longSecret = "abcdef1234567890uvwxyz";
        var maskedLong = SecurityMasker.MaskSecret(longSecret);
        Assert.StartsWith("abc", maskedLong);
        Assert.EndsWith("xyz", maskedLong);
        Assert.DoesNotContain("1234567890", maskedLong);

        Assert.Equal("<empty>", SecurityMasker.MaskSecret(null));
        Assert.Equal("<empty>", SecurityMasker.MaskSecret("   "));
    }

    [Fact]
    public void SecurityMasker_SanitizeLogMessage_RedactsAuthorizationAndSecrets()
    {
        var rawLog1 = "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.payload.sig";
        var sanitized1 = SecurityMasker.SanitizeLogMessage(rawLog1);
        Assert.DoesNotContain("eyJhbGci", sanitized1);
        Assert.Contains("Bearer [REDACTED]", sanitized1);

        var rawLog2 = "Sending payload: {\"deviceId\": \"123\", \"deviceSecret\": \"secret_value_999\"}";
        var sanitized2 = SecurityMasker.SanitizeLogMessage(rawLog2);
        Assert.DoesNotContain("secret_value_999", sanitized2);
        Assert.Contains("\"deviceSecret\": \"[REDACTED]\"", sanitized2);
    }

    [Fact]
    public void DpapiCredentialProtection_Roundtrip_WorksOnWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var dpapi = new DpapiCredentialProtection(NullLogger<DpapiCredentialProtection>.Instance);
        var plain = System.Text.Encoding.UTF8.GetBytes("secure_dpapi_token_payload");

        var cipher = dpapi.Protect(plain);
        Assert.NotEqual(plain, cipher);

        var decrypted = dpapi.Unprotect(cipher);
        Assert.Equal(plain, decrypted);
    }
}
