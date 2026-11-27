
using System.Net;
using GamingCafe.Api.Configuration;
using GamingCafe.Api.DTOs;
using GamingCafe.Api.Models;
using GamingCafe.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GamingCafe.Api.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class DeviceStatusIntegrationTests
    : IClassFixture<PostgreSqlTestFixture>
{
    private readonly PostgreSqlTestFixture _fixture;

    private readonly IOptions<HeartbeatOptions>
        _heartbeatOptions;

    private static readonly IPAddress TestIp =
        IPAddress.Parse("192.168.1.100");

    public DeviceStatusIntegrationTests(
        PostgreSqlTestFixture fixture)
    {
        _fixture = fixture;

        _heartbeatOptions = Options.Create(
            new HeartbeatOptions
            {
                IntervalSeconds = 5,
                TimeoutSeconds = 15
            });
    }

    // ============================================
    // Helper: Create heartbeat message
    // ============================================

    private static HeartbeatMessage CreateHeartbeat(
        Guid deviceId,
        long sequenceNumber = 1)
    {
        return new HeartbeatMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),

            Version = "1.0",

            MessageType = "Heartbeat",

            TimestampUtc = DateTimeOffset.UtcNow,

            DeviceId = deviceId,

            SequenceNumber = sequenceNumber,

            AgentVersion = "1.0.0",

            UpTimeSeconds = 120,

            CpuPercent = 25.5,

            MemoryUsageMb = 512
        };
    }

    // ============================================
    // Helper: Record heartbeat in PostgreSQL
    // ============================================

    private async Task<HeartbeatAcknowledgment>
        RecordHeartbeatAsync(
            Guid deviceId,
            long sequenceNumber = 1)
    {
        await using var db =
            _fixture.CreateDbContext();

        var service =
            new HeartbeatService(db);

        return await service.RecordAsync(
            CreateHeartbeat(
                deviceId,
                sequenceNumber),
            TestIp,
            CancellationToken.None);
    }

    // ============================================
    // Helper: Retrieve actual device status
    // ============================================

    private async Task<DeviceStatusResponse>
        GetStatusAsync(Guid deviceId)
    {
        await using var db =
            _fixture.CreateDbContext();

        var service = new DeviceStatusService(
            db,
            _heartbeatOptions);

        var status = await service.GetByIdAsync(
            deviceId,
            CancellationToken.None);

        Assert.NotNull(status);

        return status!;
    }

    // ============================================
    // Helper: Simulate heartbeat timeout
    // ============================================

    private async Task<DateTime>
        SimulateTimeoutAsync(Guid deviceId)
    {
        await using var db =
            _fixture.CreateDbContext();

        var status = await db.Set<DeviceStatus>()
            .SingleAsync(
                s => s.DeviceId == deviceId);

        var expiredTimestamp =
            PostgreSqlTestFixture.GetUtcNow()
                .AddSeconds(-30);

        status.LastSeenAt = expiredTimestamp;

        await db.SaveChangesAsync();

        return expiredTimestamp;
    }

    // ============================================
    // Test 1:
    // NEVER_CONNECTED -> ONLINE
    // ============================================

    [Fact]
    public async Task FirstHeartbeat_DeviceBecomesOnline()
    {
        var deviceId =
            await _fixture.CreateDeviceAsync();

        var before =
            await GetStatusAsync(deviceId);

        Assert.Equal(
            "APPROVED",
            before.RegistrationStatus);

        Assert.Equal(
            "NEVER_CONNECTED",
            before.ConnectivityStatus);

        Assert.False(before.IsOnline);

        Assert.Null(before.LastSeenAt);

        // Record the first heartbeat.

        var ack =
            await RecordHeartbeatAsync(deviceId);

        var after =
            await GetStatusAsync(deviceId);

        Assert.Equal(
            "ONLINE",
            after.ConnectivityStatus);

        Assert.True(after.IsOnline);

        Assert.NotNull(after.LastSeenAt);

        Assert.Equal(
            ack.ServerTimestampUtc.UtcDateTime,
            after.LastSeenAt.Value);

        Assert.Equal(
            TestIp.ToString(),
            after.LastIpAddress);

        Assert.Equal(
            5,
            after.HeartbeatIntervalSeconds);

        Assert.Equal(
            15,
            after.OfflineTimeoutSeconds);

        Assert.NotNull(
            after.SecondsSinceLastHeartbeat);

        Assert.InRange(
            after.SecondsSinceLastHeartbeat.Value,
            0,
            15);
    }

    // ============================================
    // Test 2:
    // ONLINE -> OFFLINE
    // ============================================

    [Fact]
    public async Task HeartbeatTimeout_DeviceBecomesOffline()
    {
        var deviceId =
            await _fixture.CreateDeviceAsync();

        await RecordHeartbeatAsync(deviceId);

        var online =
            await GetStatusAsync(deviceId);

        Assert.True(online.IsOnline);

        Assert.Equal(
            "ONLINE",
            online.ConnectivityStatus);

        // Simulate a heartbeat older than 15 seconds.

        var expiredTimestamp =
            await SimulateTimeoutAsync(deviceId);

        var offline =
            await GetStatusAsync(deviceId);

        Assert.Equal(
            "OFFLINE",
            offline.ConnectivityStatus);

        Assert.False(offline.IsOnline);

        Assert.Equal(
            "APPROVED",
            offline.RegistrationStatus);

        Assert.NotNull(offline.LastSeenAt);

        Assert.Equal(
            expiredTimestamp,
            offline.LastSeenAt.Value);

        Assert.NotNull(
            offline.SecondsSinceLastHeartbeat);

        Assert.True(
            offline.SecondsSinceLastHeartbeat.Value
                >= 15);

        // Confirm heartbeat history is preserved.

        await using var db =
            _fixture.CreateDbContext();

        var heartbeatCount =
            await db.Set<DeviceHeartbeat>()
                .CountAsync(
                    h => h.DeviceId == deviceId);

        Assert.Equal(1, heartbeatCount);
    }

    // ============================================
    // Test 3:
    // OFFLINE -> ONLINE recovery
    // ============================================

    [Fact]
    public async Task NewHeartbeat_OfflineDeviceRecovers()
    {
        var deviceId =
            await _fixture.CreateDeviceAsync();

        await RecordHeartbeatAsync(
            deviceId,
            sequenceNumber: 1);

        var expiredTimestamp =
            await SimulateTimeoutAsync(deviceId);

        var offline =
            await GetStatusAsync(deviceId);

        Assert.False(offline.IsOnline);

        Assert.Equal(
            "OFFLINE",
            offline.ConnectivityStatus);

        // Receive a new heartbeat.

        var ack =
            await RecordHeartbeatAsync(
                deviceId,
                sequenceNumber: 2);

        var recovered =
            await GetStatusAsync(deviceId);

        Assert.True(recovered.IsOnline);

        Assert.Equal(
            "ONLINE",
            recovered.ConnectivityStatus);

        Assert.Equal(
            "APPROVED",
            recovered.RegistrationStatus);

        Assert.NotNull(recovered.LastSeenAt);

        Assert.True(
            recovered.LastSeenAt.Value
                > expiredTimestamp);

        Assert.Equal(
            ack.ServerTimestampUtc.UtcDateTime,
            recovered.LastSeenAt.Value);

        Assert.NotNull(
            recovered.SecondsSinceLastHeartbeat);

        Assert.InRange(
            recovered.SecondsSinceLastHeartbeat.Value,
            0,
            15);

        // Verify both heartbeat records exist.

        await using var db =
            _fixture.CreateDbContext();

        var heartbeatCount =
            await db.Set<DeviceHeartbeat>()
                .CountAsync(
                    h => h.DeviceId == deviceId);

        Assert.Equal(2, heartbeatCount);
    }
}
