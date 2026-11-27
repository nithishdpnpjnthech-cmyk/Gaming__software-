using GamingCafe.Api.Configuration;
using Xunit;

namespace GamingCafe.Api.Tests.Configuration;

public sealed class HeartbeatOptionsTests
{
    private readonly HeartbeatOptions _options = new()
    {
        IntervalSeconds = 5,
        TimeoutSeconds = 15
    };

    private static readonly DateTime NowUtc =
        new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void DefaultInterval_ShouldBeFiveSeconds()
    {
        var options = new HeartbeatOptions();

        Assert.Equal(5, options.IntervalSeconds);
    }

    [Fact]
    public void DefaultTimeout_ShouldBeFifteenSeconds()
    {
        var options = new HeartbeatOptions();

        Assert.Equal(15, options.TimeoutSeconds);
    }

    [Fact]
    public void NoHeartbeat_ShouldNotBeOnline()
    {
        var result = _options.IsOnline(null, NowUtc);

        Assert.False(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(14)]
    [InlineData(14.999)]
    public void RecentHeartbeat_ShouldBeOnline(
        double elapsedSeconds)
    {
        var lastSeen = NowUtc.AddSeconds(-elapsedSeconds);

        var result = _options.IsOnline(lastSeen, NowUtc);

        Assert.True(result);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(15.001)]
    [InlineData(16)]
    [InlineData(30)]
    [InlineData(60)]
    public void ExpiredHeartbeat_ShouldBeOffline(
        double elapsedSeconds)
    {
        var lastSeen = NowUtc.AddSeconds(-elapsedSeconds);

        var result = _options.IsOnline(lastSeen, NowUtc);

        Assert.False(result);
    }

    [Fact]
    public void NewHeartbeatAfterTimeout_ShouldRestoreOnline()
    {
        var previousHeartbeat = NowUtc.AddSeconds(-30);

        Assert.False(
            _options.IsOnline(previousHeartbeat, NowUtc));

        var newHeartbeat = NowUtc;

        Assert.True(
            _options.IsOnline(newHeartbeat, NowUtc));
    }

    [Fact]
    public void Heartbeat_ShouldBecomeOfflineAtTimeout()
    {
        var lastSeen = NowUtc;

        Assert.True(
            _options.IsOnline(
                lastSeen,
                NowUtc.AddSeconds(5)));

        Assert.True(
            _options.IsOnline(
                lastSeen,
                NowUtc.AddSeconds(10)));

        Assert.False(
            _options.IsOnline(
                lastSeen,
                NowUtc.AddSeconds(15)));
    }
}