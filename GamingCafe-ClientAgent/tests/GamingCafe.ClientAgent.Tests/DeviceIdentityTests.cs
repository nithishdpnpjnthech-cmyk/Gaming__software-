using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GamingCafe.ClientAgent.Tests;

public class DeviceIdentityTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly IConfigurationService _mockConfig;

    public DeviceIdentityTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "AgentTest_Identity_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);

        var config = new AgentConfiguration();
        config.Identity.StorageDirectory = _tempDirectory;
        _mockConfig = new MockConfigurationService(config);
    }

    [Fact]
    public void GetOrCreateIdentity_GeneratesPersistentGuid()
    {
        var store = new DeviceIdentityStore(_mockConfig, NullLogger<DeviceIdentityStore>.Instance);
        var service = new DeviceIdentityService(store, _mockConfig, NullLogger<DeviceIdentityService>.Instance);

        var identity1 = service.GetOrCreateIdentity();
        Assert.NotEqual(Guid.Empty, identity1.DeviceId);
        Assert.False(string.IsNullOrWhiteSpace(identity1.DeviceName));

        // Reload fresh from store
        var store2 = new DeviceIdentityStore(_mockConfig, NullLogger<DeviceIdentityStore>.Instance);
        var identity2 = store2.LoadIdentity();

        Assert.NotNull(identity2);
        Assert.Equal(identity1.DeviceId, identity2.DeviceId);
        Assert.Equal(identity1.HardwareIdentifier, identity2.HardwareIdentifier);
    }

    [Fact]
    public void CorruptedIdentityFile_IsQuarantined_AndNewGenerated()
    {
        var store = new DeviceIdentityStore(_mockConfig, NullLogger<DeviceIdentityStore>.Instance);
        var service = new DeviceIdentityService(store, _mockConfig, NullLogger<DeviceIdentityService>.Instance);

        var identity1 = service.GetOrCreateIdentity();
        var idPath = Path.Combine(_tempDirectory, "device-identity.json");

        // Corrupt file contents
        File.WriteAllText(idPath, "INVALID_CORRUPTED_JSON_DATA{{{");

        var store2 = new DeviceIdentityStore(_mockConfig, NullLogger<DeviceIdentityStore>.Instance);
        var service2 = new DeviceIdentityService(store2, _mockConfig, NullLogger<DeviceIdentityService>.Instance);

        var identity2 = service2.GetOrCreateIdentity();

        Assert.NotNull(identity2);
        Assert.NotEqual(identity1.DeviceId, identity2.DeviceId);

        // Check quarantine file was created
        var quarantined = Directory.GetFiles(_tempDirectory, "device-identity.json.corrupted.*");
        Assert.NotEmpty(quarantined);
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
        catch { /* ignore cleanup error */ }
    }
}
