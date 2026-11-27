using GamingCafe.Api.Configuration;
using GamingCafe.Api.Data;
using GamingCafe.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GamingCafe.Api.Services;

public sealed class DeviceOfflineMonitorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<HeartbeatOptions> _options;
    private readonly ILogger<DeviceOfflineMonitorService> _logger;
    private readonly IDeviceMonitoringConsole? _monitoringConsole;

    public DeviceOfflineMonitorService(
        IServiceProvider serviceProvider,
        IOptionsMonitor<HeartbeatOptions> options,
        ILogger<DeviceOfflineMonitorService> logger,
        IDeviceMonitoringConsole? monitoringConsole = null)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
        _monitoringConsole = monitoringConsole;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DeviceOfflineMonitorService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

                var timeoutSeconds = _options.CurrentValue.TimeoutSeconds;
                if (timeoutSeconds <= 0) timeoutSeconds = 45;

                var thresholdUtc = DateTime.UtcNow.AddSeconds(-timeoutSeconds);

                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GamingCafeDbContext>();

                // Devices that sent a heartbeat earlier, but their last_seen_at has timed out.
                // We check if their last status change was before their last_seen_at (meaning they transitioned to active,
                // but haven't yet been transitioned to offline since then).
                var timedOutStatuses = await db.DeviceStatuses
                    .Include(s => s.Device)
                    .Where(s => s.LastSeenAt != null && s.LastSeenAt < thresholdUtc &&
                                (s.LastStatusChangeAt == null || s.LastStatusChangeAt <= s.LastSeenAt))
                    .ToListAsync(stoppingToken);

                if (timedOutStatuses.Count > 0)
                {
                    var now = DateTime.UtcNow;
                    foreach (var status in timedOutStatuses)
                    {
                        status.LastStatusChangeAt = now;
                        status.UpdatedAt = now;

                        db.DeviceConnectionLogs.Add(new DeviceConnectionLog
                        {
                            DeviceId = status.DeviceId,
                            EventType = "DISCONNECTED",
                            EventTime = now,
                            Details = $"Heartbeat timeout exceeded ({timeoutSeconds}s). Device offline."
                        });

                        _logger.LogWarning("[OFFLINE] Device marked offline after timeout. BackendDeviceId: {DeviceId}, LastSeenUtc: {LastSeenUtc}, TimeoutSeconds: {Timeout}s",
                            status.DeviceId, status.LastSeenAt, timeoutSeconds);

                        _monitoringConsole?.LogDeviceOffline(
                            status.DeviceId,
                            status.Device?.DeviceCode,
                            status.LastSeenAt,
                            timeoutSeconds);
                    }

                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in DeviceOfflineMonitorService cycle.");
            }
        }

        _logger.LogInformation("DeviceOfflineMonitorService stopped.");
    }
}
