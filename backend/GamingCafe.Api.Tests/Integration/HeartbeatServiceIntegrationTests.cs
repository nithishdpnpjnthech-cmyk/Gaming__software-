
using System.Data.Common;
using System.Net;
using GamingCafe.Api.DTOs;
using GamingCafe.Api.Models;
using GamingCafe.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GamingCafe.Api.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class HeartbeatServiceIntegrationTests
    : IClassFixture<PostgreSqlTestFixture>
{
    private readonly PostgreSqlTestFixture _fixture;

    private static readonly IPAddress TestIp =
        IPAddress.Parse("192.168.1.100");

    public HeartbeatServiceIntegrationTests(
        PostgreSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    // ============================================
    // Helper: Create heartbeat request
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
    // Helper: Record heartbeat
    // ============================================

    private async Task<HeartbeatAcknowledgment> RecordAsync(
        HeartbeatMessage message)
    {
        await using var db = _fixture.CreateDbContext();

        var service = new HeartbeatService(db);

        return await service.RecordAsync(
            message,
            TestIp,
            CancellationToken.None);
    }

    // ============================================
    // Test 1: Approved device heartbeat accepted
    // ============================================

    [Fact]
    public async Task ApprovedDevice_HeartbeatIsAccepted()
    {
        var deviceId =
            await _fixture.CreateDeviceAsync();

        var message = CreateHeartbeat(deviceId);

        var ack = await RecordAsync(message);

        Assert.NotNull(ack);
        Assert.Equal(deviceId, ack.DeviceId);
    }

    // ============================================
    // Test 2: Heartbeat history inserted
    // ============================================

    [Fact]
    public async Task Heartbeat_CreatesHistoryRecord()
    {
        var deviceId =
            await _fixture.CreateDeviceAsync();

        await RecordAsync(CreateHeartbeat(deviceId));

        await using var db = _fixture.CreateDbContext();

        var history = await db.Set<DeviceHeartbeat>()
            .AsNoTracking()
            .SingleAsync(h => h.DeviceId == deviceId);

        Assert.True(history.Id > 0);
        Assert.Equal(deviceId, history.DeviceId);
    }

    // ============================================
    // Test 3: Last seen timestamp updated
    // ============================================

    [Fact]
    public async Task Heartbeat_UpdatesLastSeenTimestamp()
    {
        var deviceId =
            await _fixture.CreateDeviceAsync();

        var ack =
            await RecordAsync(CreateHeartbeat(deviceId));

        await using var db = _fixture.CreateDbContext();

        var status = await db.Set<DeviceStatus>()
            .AsNoTracking()
            .SingleAsync(s => s.DeviceId == deviceId);

        Assert.NotNull(status.LastSeenAt);

        Assert.Equal(
            ack.ServerTimestampUtc.UtcDateTime,
            status.LastSeenAt.Value);
    }

    // ============================================
    // Test 4: Source IP address stored
    // ============================================

    [Fact]
    public async Task Heartbeat_StoresSourceIpAddress()
    {
        var deviceId =
            await _fixture.CreateDeviceAsync();

        await RecordAsync(CreateHeartbeat(deviceId));

        await using var db = _fixture.CreateDbContext();

        var history = await db.Set<DeviceHeartbeat>()
            .AsNoTracking()
            .SingleAsync(h => h.DeviceId == deviceId);

        var status = await db.Set<DeviceStatus>()
            .AsNoTracking()
            .SingleAsync(s => s.DeviceId == deviceId);

        Assert.Equal(TestIp, history.SourceIp);
        Assert.Equal(TestIp, status.LastIpAddress);
    }

    // ============================================
    // Test 5: Agent version stored
    // ============================================

    [Fact]
    public async Task Heartbeat_StoresAgentVersion()
    {
        var deviceId =
            await _fixture.CreateDeviceAsync();

        var message = CreateHeartbeat(deviceId);

        message.AgentVersion = "2.0.0";

        await RecordAsync(message);

        await using var db = _fixture.CreateDbContext();

        var history = await db.Set<DeviceHeartbeat>()
            .AsNoTracking()
            .SingleAsync(h => h.DeviceId == deviceId);

        Assert.Equal("2.0.0", history.AgentVersion);
    }

    // ============================================
    // Test 6: Acknowledgement contract
    // ============================================

    [Fact]
    public async Task Heartbeat_ReturnsCorrectAcknowledgment()
    {
        var deviceId =
            await _fixture.CreateDeviceAsync();

        var message = CreateHeartbeat(deviceId, 10);

        var ack = await RecordAsync(message);

        Assert.False(
            string.IsNullOrWhiteSpace(ack.MessageId));

        Assert.Equal("1.0", ack.Version);

        Assert.Equal(
            "HeartbeatAck",
            ack.MessageType);

        Assert.Equal(deviceId, ack.DeviceId);

        Assert.Equal(
            message.SequenceNumber,
            ack.SequenceNumber);

        Assert.Equal(
            message.MessageId,
            ack.OriginalMessageId);

        Assert.Equal(
            ack.TimestampUtc,
            ack.ServerTimestampUtc);

        Assert.Equal(
            TimeSpan.Zero,
            ack.ServerTimestampUtc.Offset);
    }

    // ============================================
    // Test 7: PostgreSQL timestamp precision
    // ============================================

    [Fact]
    public async Task Heartbeat_TimestampsMatchExactly()
    {
        var deviceId =
            await _fixture.CreateDeviceAsync();

        var ack =
            await RecordAsync(CreateHeartbeat(deviceId));

        await using var db = _fixture.CreateDbContext();

        var history = await db.Set<DeviceHeartbeat>()
            .AsNoTracking()
            .SingleAsync(h => h.DeviceId == deviceId);

        var status = await db.Set<DeviceStatus>()
            .AsNoTracking()
            .SingleAsync(s => s.DeviceId == deviceId);

        var expected =
            ack.ServerTimestampUtc.UtcDateTime;

        Assert.Equal(expected, history.ReceivedAt);

        Assert.Equal(
            expected,
            status.LastSeenAt!.Value);

        Assert.Equal(
            0L,
            expected.Ticks % 10);
    }

    // ============================================
    // Test 8: Multiple heartbeats
    // ============================================

    [Fact]
    public async Task MultipleHeartbeats_CreateHistoryRecords()
    {
        var deviceId =
            await _fixture.CreateDeviceAsync();

        await RecordAsync(CreateHeartbeat(deviceId, 1));
        await RecordAsync(CreateHeartbeat(deviceId, 2));

        var lastAck =
            await RecordAsync(CreateHeartbeat(deviceId, 3));

        await using var db = _fixture.CreateDbContext();

        var count = await db.Set<DeviceHeartbeat>()
            .CountAsync(h => h.DeviceId == deviceId);

        var status = await db.Set<DeviceStatus>()
            .AsNoTracking()
            .SingleAsync(s => s.DeviceId == deviceId);

        Assert.Equal(3, count);

        Assert.Equal(
            lastAck.ServerTimestampUtc.UtcDateTime,
            status.LastSeenAt!.Value);
    }

    // ============================================
    // Tests 9, 10, 11: Non-approved devices
    // ============================================

    [Theory]
    [InlineData("PENDING")]
    [InlineData("REJECTED")]
    [InlineData("REVOKED")]
    public async Task NonApprovedDevice_HeartbeatIsRejected(
        string registrationStatus)
    {
        var deviceId =
            await _fixture.CreateDeviceAsync(
                registrationStatus);

        var message = CreateHeartbeat(deviceId);

        await Assert.ThrowsAsync<HubException>(
            () => RecordAsync(message));

        await using var db = _fixture.CreateDbContext();

        var count = await db.Set<DeviceHeartbeat>()
            .CountAsync(h => h.DeviceId == deviceId);

        var status = await db.Set<DeviceStatus>()
            .AsNoTracking()
            .SingleAsync(s => s.DeviceId == deviceId);

        Assert.Equal(0, count);
        Assert.Null(status.LastSeenAt);
    }

    // ============================================
    // Test 12: Unknown device
    // ============================================

    [Fact]
    public async Task UnknownDevice_HeartbeatIsRejected()
    {
        var deviceId = Guid.NewGuid();

        await Assert.ThrowsAsync<HubException>(
            () => RecordAsync(CreateHeartbeat(deviceId)));

        await using var db = _fixture.CreateDbContext();

        var exists = await db.Set<DeviceHeartbeat>()
            .AnyAsync(h => h.DeviceId == deviceId);

        Assert.False(exists);
    }

    // ============================================
    // Test 13: Transaction rollback
    // ============================================

    [Fact]
    public async Task StatusUpdateFailure_RollsBackHeartbeat()
    {
        var deviceId =
            await _fixture.CreateDeviceAsync();

        var interceptor =
            new StatusUpdateFailureInterceptor();

        await using (
            var db = _fixture.CreateDbContext(interceptor))
        {
            var service = new HeartbeatService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.RecordAsync(
                    CreateHeartbeat(deviceId),
                    TestIp,
                    CancellationToken.None));
        }

        Assert.True(interceptor.FailureTriggered);

        await using var verificationDb =
            _fixture.CreateDbContext();

        var count =
            await verificationDb.Set<DeviceHeartbeat>()
                .CountAsync(h => h.DeviceId == deviceId);

        var status =
            await verificationDb.Set<DeviceStatus>()
                .AsNoTracking()
                .SingleAsync(s => s.DeviceId == deviceId);

        Assert.Equal(0, count);
        Assert.Null(status.LastSeenAt);
    }

    // ============================================
    // Test 14: Missing status record created
    // ============================================

    [Fact]
    public async Task MissingDeviceStatus_IsCreated()
    {
        var deviceId =
            await _fixture.CreateDeviceAsync(
                createStatus: false);

        await RecordAsync(CreateHeartbeat(deviceId));

        await using var db = _fixture.CreateDbContext();

        var status = await db.Set<DeviceStatus>()
            .AsNoTracking()
            .SingleAsync(s => s.DeviceId == deviceId);

        Assert.Equal(deviceId, status.DeviceId);
        Assert.NotNull(status.LastSeenAt);
        Assert.Equal(TestIp, status.LastIpAddress);
    }

    // ============================================
    // Test 15: Existing power state preserved
    // ============================================

    [Fact]
    public async Task Heartbeat_PreservesReportedPowerState()
    {
        var deviceId =
            await _fixture.CreateDeviceAsync();

        var originalChangeTime =
            PostgreSqlTestFixture.GetUtcNow()
                .AddMinutes(-5);

        await using (var db = _fixture.CreateDbContext())
        {
            var status = await db.Set<DeviceStatus>()
                .SingleAsync(s => s.DeviceId == deviceId);

            status.ReportedPowerState = "ON";

            status.LastStatusChangeAt =
                originalChangeTime;

            await db.SaveChangesAsync();
        }

        await RecordAsync(CreateHeartbeat(deviceId));

        await using var verificationDb =
            _fixture.CreateDbContext();

        var updatedStatus =
            await verificationDb.Set<DeviceStatus>()
                .AsNoTracking()
                .SingleAsync(s => s.DeviceId == deviceId);

        Assert.Equal(
            "ON",
            updatedStatus.ReportedPowerState);

        Assert.Equal(
            originalChangeTime,
            updatedStatus.LastStatusChangeAt!.Value);

        Assert.NotNull(updatedStatus.LastSeenAt);
    }

    // ============================================
    // Test 16: Concurrent heartbeat processing
    // ============================================

    [Fact]
    public async Task ConcurrentHeartbeats_ArePersistedCorrectly()
    {
        var deviceId =
            await _fixture.CreateDeviceAsync();

        const int heartbeatCount = 5;

        var tasks = Enumerable
            .Range(1, heartbeatCount)
            .Select(sequence =>
                RecordAsync(
                    CreateHeartbeat(deviceId, sequence)))
            .ToArray();

        var acknowledgments =
            await Task.WhenAll(tasks);

        await using var db = _fixture.CreateDbContext();

        var history = await db.Set<DeviceHeartbeat>()
            .AsNoTracking()
            .Where(h => h.DeviceId == deviceId)
            .ToListAsync();

        var status = await db.Set<DeviceStatus>()
            .AsNoTracking()
            .SingleAsync(s => s.DeviceId == deviceId);

        Assert.Equal(heartbeatCount, history.Count);

        Assert.Equal(
            heartbeatCount,
            acknowledgments.Length);

        var latestTimestamp =
            history.Max(h => h.ReceivedAt);

        Assert.Equal(
            latestTimestamp,
            status.LastSeenAt!.Value);

        Assert.Equal(
            acknowledgments.Max(
                a => a.ServerTimestampUtc.UtcDateTime),
            latestTimestamp);
    }

    // ============================================
    // Interceptor: Simulate status update failure
    // ============================================

    private sealed class StatusUpdateFailureInterceptor
        : DbCommandInterceptor
    {
        public bool FailureTriggered { get; private set; }

        public override ValueTask<InterceptionResult<int>>
            NonQueryExecutingAsync(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains(
                    "INSERT INTO public.device_status",
                    StringComparison.OrdinalIgnoreCase))
            {
                FailureTriggered = true;

                throw new InvalidOperationException(
                    "Simulated device status update failure.");
            }

            return base.NonQueryExecutingAsync(
                command,
                eventData,
                result,
                cancellationToken);
        }
    }
}
