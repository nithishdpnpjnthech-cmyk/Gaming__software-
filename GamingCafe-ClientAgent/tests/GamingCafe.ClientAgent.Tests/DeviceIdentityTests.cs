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

    [Fact]
    public void MultipleAgentInstances_RetainSeparateIdentities_AndUniqueDeviceCodes()
    {
        var tempDirA = Path.Combine(Path.GetTempPath(), "AgentTest_InstA_" + Guid.NewGuid().ToString("N"));
        var tempDirB = Path.Combine(Path.GetTempPath(), "AgentTest_InstB_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirA);
        Directory.CreateDirectory(tempDirB);

        try
        {
            var configA = new AgentConfiguration();
            configA.Identity.StorageDirectory = tempDirA;
            var mockConfigA = new MockConfigurationService(configA);
            var storeA = new DeviceIdentityStore(mockConfigA, NullLogger<DeviceIdentityStore>.Instance);
            var serviceA = new DeviceIdentityService(storeA, mockConfigA, NullLogger<DeviceIdentityService>.Instance);

            var configB = new AgentConfiguration();
            configB.Identity.StorageDirectory = tempDirB;
            var mockConfigB = new MockConfigurationService(configB);
            var storeB = new DeviceIdentityStore(mockConfigB, NullLogger<DeviceIdentityStore>.Instance);
            var serviceB = new DeviceIdentityService(storeB, mockConfigB, NullLogger<DeviceIdentityService>.Instance);

            var identityA = serviceA.GetOrCreateIdentity();
            var identityB = serviceB.GetOrCreateIdentity();

            Assert.NotEqual(Guid.Empty, identityA.DeviceId);
            Assert.NotEqual(Guid.Empty, identityB.DeviceId);
            Assert.NotEqual(identityA.DeviceId, identityB.DeviceId);
            Assert.NotEqual(identityA.DeviceCode, identityB.DeviceCode);
            Assert.StartsWith("PC-", identityA.DeviceCode);
            Assert.StartsWith("PC-", identityB.DeviceCode);

            // Persisted independently
            var reloadedA = storeA.LoadIdentity();
            var reloadedB = storeB.LoadIdentity();

            Assert.NotNull(reloadedA);
            Assert.NotNull(reloadedB);
            Assert.Equal(identityA.DeviceId, reloadedA.DeviceId);
            Assert.Equal(identityB.DeviceId, reloadedB.DeviceId);
            Assert.Equal(identityA.DeviceCode, reloadedA.DeviceCode);
            Assert.Equal(identityB.DeviceCode, reloadedB.DeviceCode);
        }
        finally
        {
            try { Directory.Delete(tempDirA, true); } catch { }
            try { Directory.Delete(tempDirB, true); } catch { }
        }
    }

    [Fact]
    public void StoragePathResolver_ResolvesRootedPathDirectly()
    {
        var customPath = Path.Combine(Path.GetTempPath(), "CustomDirectDir");
        var resolved = StoragePathResolver.ResolveStorageDirectory(customPath, NullLogger.Instance);
        Assert.Equal(Path.GetFullPath(customPath), resolved);
    }

    [Fact]
    public void StoragePathResolver_ResolvesCurrentDirectoryWhenFolderExists()
    {
        var testSubdir = Path.Combine(Directory.GetCurrentDirectory(), "test-data-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testSubdir);

        try
        {
            var relativeName = Path.GetFileName(testSubdir);
            var resolved = StoragePathResolver.ResolveStorageDirectory(relativeName, NullLogger.Instance);
            Assert.Equal(Path.GetFullPath(testSubdir), resolved);
        }
        finally
        {
            try { Directory.Delete(testSubdir, true); } catch { }
        }
    }

    [Fact]
    public void DeviceIdentityStore_ExposesResolvedPaths()
    {
        var store = new DeviceIdentityStore(_mockConfig, NullLogger<DeviceIdentityStore>.Instance);
        Assert.Equal(Path.GetFullPath(_tempDirectory), store.StorageDirectory);
        Assert.Equal(Path.Combine(Path.GetFullPath(_tempDirectory), "device-identity.json"), store.IdentityFilePath);
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
