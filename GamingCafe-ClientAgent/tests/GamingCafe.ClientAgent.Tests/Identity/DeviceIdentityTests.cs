using GamingCafe.ClientAgent.Identity;
using GamingCafe.ClientAgent.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GamingCafe.ClientAgent.Tests.Identity;

public class DeviceIdentityTests
{
    [Fact]
    public async Task GetOrCreateIdentityAsync_InitialRun_GeneratesValidUniqueIdentity()
    {
        var store = new TestDeviceIdentityStore();
        var service = new DeviceIdentityService(store, NullLogger<DeviceIdentityService>.Instance);

        var identity = await service.GetOrCreateIdentityAsync();

        Assert.NotEqual(Guid.Empty, identity.DeviceId);
        Assert.False(string.IsNullOrWhiteSpace(identity.Hostname));
        Assert.False(string.IsNullOrWhiteSpace(identity.OperatingSystem));
        Assert.True(identity.CreatedAtUtc <= DateTime.UtcNow);
        Assert.True(store.Exists());
    }

    [Fact]
    public async Task GetOrCreateIdentityAsync_SubsequentCall_ReusesExistingIdentityWithoutRegeneration()
    {
        var store = new TestDeviceIdentityStore();
        var service1 = new DeviceIdentityService(store, NullLogger<DeviceIdentityService>.Instance);
        var identity1 = await service1.GetOrCreateIdentityAsync();

        var service2 = new DeviceIdentityService(store, NullLogger<DeviceIdentityService>.Instance);
        var identity2 = await service2.GetOrCreateIdentityAsync();

        Assert.Equal(identity1.DeviceId, identity2.DeviceId);
        Assert.Equal(identity1.Hostname, identity2.Hostname);
        Assert.Equal(identity1.CreatedAtUtc, identity2.CreatedAtUtc);
    }

    [Fact]
    public async Task GetOrCreateIdentityAsync_CorruptIdentityFile_RecoversSafelyAndGeneratesNewIdentity()
    {
        var store = new TestDeviceIdentityStore();
        store.ExistsFlag = true; // Mark as existing without seeding valid data
        var service = new DeviceIdentityService(store, NullLogger<DeviceIdentityService>.Instance);

        var identity = await service.GetOrCreateIdentityAsync();

        Assert.NotNull(identity);
        Assert.NotEqual(Guid.Empty, identity.DeviceId);
        Assert.True(store.CorruptStorageRecovered);
        Assert.True(store.Exists());
    }
}
