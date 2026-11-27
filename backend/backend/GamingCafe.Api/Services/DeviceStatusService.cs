using GamingCafe.Api.Configuration;
using GamingCafe.Api.Data;
using GamingCafe.Api.DTOs;
using GamingCafe.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GamingCafe.Api.Services;

public sealed class DeviceStatusService
{
    private readonly GamingCafeDbContext _db;
    private readonly HeartbeatOptions _options;

    public DeviceStatusService(
        GamingCafeDbContext db,
        IOptions<HeartbeatOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    private string ResolveConnectivityStatus(
    string registrationStatus,
    DateTime? lastSeenAt,
    DateTime now)
{
    return DeviceConnectivityResolver.Resolve(
        registrationStatus,
        lastSeenAt,
        now,
        _options);
}

    private DeviceStatusResponse Map(
        Device device,
        DateTime now)
    {
        var status = device.DeviceStatus;

        var connectivity = ResolveConnectivityStatus(
            device.RegistrationStatus,
            status?.LastSeenAt,
            now);

        return new DeviceStatusResponse
        {
            DeviceId = device.Id,
            DeviceCode = device.DeviceCode,
            DisplayName = device.DisplayName,
            Hostname = device.Hostname,
            AgentVersion = device.AgentVersion,
            RegistrationStatus = device.RegistrationStatus,

            ConnectivityStatus = connectivity,
            IsOnline = connectivity == "ONLINE",

            LastSeenAt = status?.LastSeenAt,

            LastIpAddress =
                status?.LastIpAddress?.ToString(),

            ReportedPowerState =
                status?.ReportedPowerState,

            LastStatusChangeAt =
                status?.LastStatusChangeAt,

            SecondsSinceLastHeartbeat =
                status?.LastSeenAt is DateTime lastSeen
                    ? Math.Round(
                        Math.Max(0, (now - lastSeen).TotalSeconds),
                        2)
                    : null,

            HeartbeatIntervalSeconds =
                _options.IntervalSeconds,

            OfflineTimeoutSeconds =
                _options.TimeoutSeconds,

            ServerTimestampUtc = now
        };
    }

    public async Task<List<DeviceStatusResponse>> GetAllAsync(
        CancellationToken ct)
    {
        var devices = await _db.Devices
            .AsNoTracking()
            .Include(d => d.DeviceStatus)
            .OrderBy(d => d.DeviceCode)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;

        return devices
            .Select(d => Map(d, now))
            .ToList();
    }

    public async Task<DeviceStatusResponse?> GetByIdAsync(
        Guid deviceId,
        CancellationToken ct)
    {
        var device = await _db.Devices
            .AsNoTracking()
            .Include(d => d.DeviceStatus)
            .SingleOrDefaultAsync(
                d => d.Id == deviceId, ct);

        return device is null
            ? null
            : Map(device, DateTime.UtcNow);
    }

    public async Task<DeviceStatusSummaryResponse> GetSummaryAsync(
        CancellationToken ct)
    {
        var devices = await _db.Devices
            .AsNoTracking()
            .Select(d => new
            {
                d.RegistrationStatus,
                LastSeenAt = d.DeviceStatus == null
                    ? null
                    : d.DeviceStatus.LastSeenAt
            })
            .ToListAsync(ct);

        var now = DateTime.UtcNow;

        var statuses = devices.Select(d =>
            ResolveConnectivityStatus(
                d.RegistrationStatus,
                d.LastSeenAt,
                now))
            .ToList();

        return new DeviceStatusSummaryResponse
        {
            TotalDevices = devices.Count,

            ApprovedDevices = devices.Count(
                d => d.RegistrationStatus == "APPROVED"),

            OnlineDevices = statuses.Count(
                s => s == "ONLINE"),

            OfflineDevices = statuses.Count(
                s => s == "OFFLINE"),

            NeverConnectedDevices = statuses.Count(
                s => s == "NEVER_CONNECTED"),

            PendingDevices = statuses.Count(
                s => s == "PENDING"),

            RejectedDevices = statuses.Count(
                s => s == "REJECTED"),

            RevokedDevices = statuses.Count(
                s => s == "REVOKED"),

            HeartbeatIntervalSeconds =
                _options.IntervalSeconds,

            OfflineTimeoutSeconds =
                _options.TimeoutSeconds,

            ServerTimestampUtc = now
        };
    }
}