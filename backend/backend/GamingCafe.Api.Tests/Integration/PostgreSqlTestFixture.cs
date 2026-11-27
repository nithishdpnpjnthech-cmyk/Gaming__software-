
using GamingCafe.Api.Data;
using GamingCafe.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using System.Collections.Concurrent;

namespace GamingCafe.Api.Tests.Integration;

public sealed class PostgreSqlTestFixture : IAsyncLifetime
{
    private const string ExpectedDatabase =
        "gaming_cafe_test_db";

    private const string ConnectionVariable =
        "GAMINGCAFE_TEST_CONNECTION_STRING";

    private readonly ConcurrentDictionary<Guid, byte>
        _createdDeviceIds = new();

    private string? _connectionString;

    private bool _databaseVerified;

    // ============================================
    // 1. Initialize and verify test database
    // ============================================

    public async Task InitializeAsync()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                ConnectionVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Environment variable {ConnectionVariable} " +
                "is missing.");
        }

        var config =
            new NpgsqlConnectionStringBuilder(
                connectionString);

        // Protect the development database.
        if (!string.Equals(
                config.Database,
                ExpectedDatabase,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Integration tests must use only " +
                ExpectedDatabase);
        }

        // Restrict testing to local PostgreSQL.
        if (!string.Equals(
                config.Host,
                "localhost",
                StringComparison.OrdinalIgnoreCase) &&
            config.Host != "127.0.0.1" &&
            config.Host != "::1")
        {
            throw new InvalidOperationException(
                "Integration tests require local PostgreSQL.");
        }

        if (config.Port != 5432)
        {
            throw new InvalidOperationException(
                "Integration tests require PostgreSQL port 5432.");
        }

        await using var connection =
            new NpgsqlConnection(connectionString);

        await connection.OpenAsync();

        await using var command =
            connection.CreateCommand();

        command.CommandText =
            "SELECT current_database();";

        var actualDatabase =
            Convert.ToString(
                await command.ExecuteScalarAsync());

        if (!string.Equals(
                actualDatabase,
                ExpectedDatabase,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Actual PostgreSQL database does not " +
                "match the isolated test database.");
        }

        _connectionString = connectionString;
        _databaseVerified = true;
    }

    // ============================================
    // 2. Create independent EF Core context
    // ============================================

    public GamingCafeDbContext CreateDbContext(
        params IInterceptor[] interceptors)
    {
        if (!_databaseVerified ||
            string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException(
                "Test database has not been verified.");
        }

        var optionsBuilder =
            new DbContextOptionsBuilder<GamingCafeDbContext>();

        optionsBuilder.UseNpgsql(_connectionString);

        if (interceptors.Length > 0)
        {
            optionsBuilder.AddInterceptors(interceptors);
        }

        return new GamingCafeDbContext(
            optionsBuilder.Options);
    }

    // ============================================
    // 3. Create temporary test device
    // ============================================

    public async Task<Guid> CreateDeviceAsync(
        string registrationStatus = "APPROVED",
        bool createStatus = true,
        CancellationToken ct = default)
    {
        var validStatuses = new[]
        {
            "PENDING",
            "APPROVED",
            "REJECTED",
            "REVOKED"
        };

        if (!validStatuses.Contains(registrationStatus))
        {
            throw new ArgumentException(
                "Invalid device registration status.",
                nameof(registrationStatus));
        }

        await using var db = CreateDbContext();

        var deviceId = Guid.NewGuid();

        var now = GetUtcNow();

        var device = new Device
        {
            Id = deviceId,

            DeviceCode =
                $"IT-{deviceId:N}",

            DisplayName =
                "Integration Test Device",

            Hostname =
                "INTEGRATION-TEST",

            AgentVersion = "1.0.0",

            RegistrationStatus =
                registrationStatus,

            ApprovedAt =
                registrationStatus == "APPROVED"
                    ? now
                    : null,

            CreatedAt = now,

            UpdatedAt = now
        };

        // Track only devices created by this fixture.
        _createdDeviceIds.TryAdd(deviceId, 0);

        db.Set<Device>().Add(device);

        if (createStatus)
        {
            db.Set<DeviceStatus>().Add(
                new DeviceStatus
                {
                    DeviceId = deviceId,

                    LastSeenAt = null,

                    LastIpAddress = null,

                    ReportedPowerState = null,

                    LastStatusChangeAt = null,

                    UpdatedAt = now
                });
        }

        await db.SaveChangesAsync(ct);

        return deviceId;
    }

    // ============================================
    // 4. PostgreSQL-compatible UTC timestamp
    // ============================================

    public static DateTime GetUtcNow()
    {
        var now = DateTime.UtcNow;

        return new DateTime(
            now.Ticks - (now.Ticks % 10),
            DateTimeKind.Utc);
    }

    // ============================================
    // 5. Clean up only fixture-created devices
    // ============================================

    public async Task DisposeAsync()
    {
        if (!_databaseVerified)
        {
            return;
        }

        try
        {
            var deviceIds =
                _createdDeviceIds.Keys.ToArray();

            if (deviceIds.Length > 0)
            {
                await using var db =
                    CreateDbContext();

                var commandIds = await db.Set<DeviceCommand>()
                    .Where(c => deviceIds.Contains(c.DeviceId))
                    .Select(c => c.Id)
                    .ToListAsync();

                if (commandIds.Count > 0)
                {
                    await db.Set<DeviceCommandLog>()
                        .Where(l => commandIds.Contains(l.CommandId))
                        .ExecuteDeleteAsync();

                    await db.Set<DeviceCommand>()
                        .Where(c => commandIds.Contains(c.Id))
                        .ExecuteDeleteAsync();
                }

                await db.Set<DeviceCredential>()
                    .Where(c => deviceIds.Contains(c.DeviceId))
                    .ExecuteDeleteAsync();

                await db.Set<DeviceConnectionLog>()
                    .Where(l => deviceIds.Contains(l.DeviceId))
                    .ExecuteDeleteAsync();

                await db.Set<DeviceHeartbeat>()
                    .Where(h => deviceIds.Contains(h.DeviceId))
                    .ExecuteDeleteAsync();

                await db.Set<DeviceStatus>()
                    .Where(s => deviceIds.Contains(s.DeviceId))
                    .ExecuteDeleteAsync();

                await db.Set<Device>()
                    .Where(d => deviceIds.Contains(d.Id))
                    .ExecuteDeleteAsync();

                _createdDeviceIds.Clear();
            }
        }
        finally
        {
            _connectionString = null;
            _databaseVerified = false;
        }
    }
}
