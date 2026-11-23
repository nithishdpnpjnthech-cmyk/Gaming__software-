using System.Runtime.InteropServices;
using GamingCafe.ClientAgent.Security;
using GamingCafe.ClientAgent.Storage;
using GamingCafe.ClientAgent.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GamingCafe.ClientAgent.Tests.Security;

public class CredentialStorageAndMaskingTests
{
    [Fact]
    public async Task InMemoryCredentialStorage_Lifecycle_SavesRetrievesAndDeletesCorrectly()
    {
        var storage = new InMemoryCredentialStorage();
        const string key = "test_key";
        const string secret = "super_secret_token_12345";

        // Initial state
        Assert.False(await storage.HasCredentialAsync(key));
        Assert.Null(await storage.GetCredentialAsync(key));

        // Save
        await storage.SaveCredentialAsync(key, secret);
        Assert.True(await storage.HasCredentialAsync(key));
        Assert.Equal(secret, await storage.GetCredentialAsync(key));

        // Overwrite
        const string newSecret = "rotated_secret_token_67890";
        await storage.SaveCredentialAsync(key, newSecret);
        Assert.Equal(newSecret, await storage.GetCredentialAsync(key));

        // Delete
        await storage.DeleteCredentialAsync(key);
        Assert.False(await storage.HasCredentialAsync(key));
        Assert.Null(await storage.GetCredentialAsync(key));
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
    public async Task WindowsDpapiCredentialStorage_Roundtrip_WorksOnWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return; // Skip on non-Windows
        }

        var fileStorage = new TestFileStorage();
        var dpapi = new WindowsDpapiCredentialStorage(
            fileStorage,
            NullLogger<WindowsDpapiCredentialStorage>.Instance);

        const string key = "device_test_key";
        const string secret = "dpapi_protected_token_value_xyz";

        await dpapi.SaveCredentialAsync(key, secret);
        Assert.True(await dpapi.HasCredentialAsync(key));

        var retrieved = await dpapi.GetCredentialAsync(key);
        Assert.Equal(secret, retrieved);

        await dpapi.DeleteCredentialAsync(key);
        Assert.False(await dpapi.HasCredentialAsync(key));
    }
}
