using GamingCafe.ClientAgent.Authentication;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GamingCafe.ClientAgent.Tests;

public class CredentialProtectionTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly IConfigurationService _mockConfig;

    public CredentialProtectionTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "AgentTest_Creds_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);

        var config = new AgentConfiguration();
        config.Identity.StorageDirectory = _tempDirectory;
        config.Security.UseMachineScope = false; // Use CurrentUser in tests
        _mockConfig = new MockConfigurationService(config);
    }

    [Fact]
    public void Dpapi_RoundTripEncryption_PreservesPlaintext()
    {
        var protector = new DpapiCredentialProtection(_mockConfig, NullLogger<DpapiCredentialProtection>.Instance);
        var plaintext = "Secret-Token-12345-XYZ-GamingCafe";

        var encrypted = protector.ProtectString(plaintext);
        Assert.NotEqual(plaintext, encrypted);

        var decrypted = protector.UnprotectString(encrypted);
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void SecureCredentialStore_SavesAndLoadsCredentialsSecurely()
    {
        var protector = new DpapiCredentialProtection(_mockConfig, NullLogger<DpapiCredentialProtection>.Instance);
        var store = new SecureCredentialStore(_mockConfig, protector, NullLogger<SecureCredentialStore>.Instance);

        var creds = new DeviceCredentials
        {
            DeviceId = Guid.NewGuid(),
            AccessToken = "jwt-access-token-abc-123",
            RefreshToken = "jwt-refresh-token-xyz-789",
            TokenExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2),
            DeviceSecret = "device-private-secret-key",
            IssuedAtUtc = DateTimeOffset.UtcNow
        };

        store.SaveCredentials(creds);

        // Raw file should not contain plaintext token
        var credsPath = Path.Combine(_tempDirectory, "device-credentials.dat");
        var rawBytes = File.ReadAllBytes(credsPath);
        var rawString = System.Text.Encoding.UTF8.GetString(rawBytes);
        Assert.DoesNotContain("jwt-access-token", rawString);

        // Load decrypted
        var loaded = store.LoadCredentials();
        Assert.NotNull(loaded);
        Assert.Equal(creds.DeviceId, loaded.DeviceId);
        Assert.Equal(creds.AccessToken, loaded.AccessToken);
        Assert.Equal(creds.RefreshToken, loaded.RefreshToken);
        Assert.True(loaded.HasValidToken);

        // Clear
        store.ClearCredentials();
        Assert.False(store.HasCredentials());
        Assert.Null(store.LoadCredentials());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch { /* ignore */ }
    }
}
