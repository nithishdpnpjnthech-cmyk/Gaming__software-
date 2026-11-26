using GamingCafe.Api.Configuration;
using GamingCafe.Api.Services;
using Xunit;

namespace GamingCafe.Api.Tests.Services;

public sealed class DeviceConnectivityResolverTests
{
    private readonly HeartbeatOptions _options = new()
    {
        IntervalSeconds = 5,
        TimeoutSeconds = 15
    };

    private static readonly DateTime NowUtc =
        new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ApprovedWithoutHeartbeat_ShouldBeNeverConnected()
    {
        var result = DeviceConnectivityResolver.Resolve(
            "APPROVED",
            null,
            NowUtc,
            _options);

        Assert.Equal("NEVER_CONNECTED", result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(14)]
    [InlineData(14.999)]
    public void ApprovedWithRecentHeartbeat_ShouldBeOnline(
        double elapsedSeconds)
    {
        var result = DeviceConnectivityResolver.Resolve(
            "APPROVED",
            NowUtc.AddSeconds(-elapsedSeconds),
            NowUtc,
            _options);

        Assert.Equal("ONLINE", result);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(15.001)]
    [InlineData(16)]
    [InlineData(30)]
    [InlineData(60)]
    public void ApprovedWithExpiredHeartbeat_ShouldBeOffline(
        double elapsedSeconds)
    {
        var result = DeviceConnectivityResolver.Resolve(
            "APPROVED",
            NowUtc.AddSeconds(-elapsedSeconds),
            NowUtc,
            _options);

        Assert.Equal("OFFLINE", result);
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("REJECTED")]
    [InlineData("REVOKED")]
    public void UnapprovedWithoutHeartbeat_ShouldPreserveStatus(
        string registrationStatus)
    {
        var result = DeviceConnectivityResolver.Resolve(
            registrationStatus,
            null,
            NowUtc,
            _options);

        Assert.Equal(registrationStatus, result);
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("REJECTED")]
    [InlineData("REVOKED")]
    public void UnapprovedWithRecentHeartbeat_ShouldPreserveStatus(
        string registrationStatus)
    {
        var result = DeviceConnectivityResolver.Resolve(
            registrationStatus,
            NowUtc.AddSeconds(-5),
            NowUtc,
            _options);

        Assert.Equal(registrationStatus, result);
    }

    [Fact]
    public void NewHeartbeat_ShouldRestoreOnlineStatus()
    {
        var offlineStatus = DeviceConnectivityResolver.Resolve(
            "APPROVED",
            NowUtc.AddSeconds(-30),
            NowUtc,
            _options);

        Assert.Equal("OFFLINE", offlineStatus);

        var onlineStatus = DeviceConnectivityResolver.Resolve(
            "APPROVED",
            NowUtc,
            NowUtc,
            _options);

        Assert.Equal("ONLINE", onlineStatus);
    }

    [Fact]
    public void ApprovedDevice_ShouldBecomeOfflineAtTimeout()
    {
        var lastSeenAt = NowUtc;

        Assert.Equal(
            "ONLINE",
            DeviceConnectivityResolver.Resolve(
                "APPROVED",
                lastSeenAt,
                NowUtc.AddSeconds(10),
                _options));

        Assert.Equal(
            "OFFLINE",
            DeviceConnectivityResolver.Resolve(
                "APPROVED",
                lastSeenAt,
                NowUtc.AddSeconds(15),
                _options));
    }
}