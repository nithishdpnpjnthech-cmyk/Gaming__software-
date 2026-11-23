using System.Diagnostics;
using GamingCafe.ClientAgent.Communication;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Health;
using GamingCafe.ClientAgent.Identity;
using GamingCafe.ClientAgent.Messaging;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Heartbeat;

public sealed class HeartbeatService : IHeartbeatService
{
    private readonly ISignalRConnectionManager _connectionManager;
    private readonly IDeviceIdentityService _identityService;
    private readonly IAgentHealthService _healthService;
    private readonly IConfigurationService _configService;
    private readonly ILogger<HeartbeatService> _logger;

    private long _sequenceNumber;
    private int _missedHeartbeats;
    private double _lastLatencyMs;
    private readonly Stopwatch _stopwatch = new();
    private readonly object _lock = new();

    public HeartbeatService(
        ISignalRConnectionManager connectionManager,
        IDeviceIdentityService identityService,
        IAgentHealthService healthService,
        IConfigurationService configService,
        ILogger<HeartbeatService> logger)
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
        _healthService = healthService ?? throw new ArgumentNullException(nameof(healthService));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public long SequenceNumber => Interlocked.Read(ref _sequenceNumber);
    public int MissedHeartbeats { get { lock (_lock) return _missedHeartbeats; } }
    public double LastLatencyMs { get { lock (_lock) return _lastLatencyMs; } }

    public async Task<bool> SendHeartbeatAsync(CancellationToken cancellationToken = default)
    {
        if (!_connectionManager.IsConnected)
        {
            _logger.LogDebug("Skipping heartbeat: SignalR is disconnected.");
            return false;
        }

        var seq = Interlocked.Increment(ref _sequenceNumber);
        var identity = _identityService.CurrentIdentity;
        var metrics = _healthService.GetSnapshot();

        var message = new HeartbeatMessage
        {
            DeviceId = identity.DeviceId,
            SequenceNumber = seq,
            AgentVersion = identity.ClientVersion,
            UpTimeSeconds = metrics.UptimeSeconds,
            CpuPercent = metrics.CpuPercent,
            MemoryUsageMb = metrics.MemoryUsageMb,
            TimestampUtc = DateTimeOffset.UtcNow
        };

        try
        {
            lock (_lock)
            {
                _stopwatch.Restart();
            }

            _logger.LogDebug("Dispatching SignalR heartbeat #{Sequence} (DeviceId: {DeviceId})", seq, identity.DeviceId);
            await _connectionManager.SendAsync("SendHeartbeat", message, cancellationToken).ConfigureAwait(false);
            _healthService.RecordHeartbeatSent();

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Heartbeat #{Sequence} dispatch cancelled due to service shutdown.", seq);
            return false;
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                _missedHeartbeats++;
            }
            _logger.LogWarning("Failed to send SignalR heartbeat #{Sequence}: {Reason}. Missed count: {MissedCount}", seq, ex.Message, _missedHeartbeats);
            _logger.LogDebug(ex, "Detailed exception for failed heartbeat #{Sequence}:", seq);

            var maxMissed = _configService.CurrentConfiguration.Heartbeat.MaxMissedHeartbeats;
            if (_missedHeartbeats >= maxMissed)
            {
                _logger.LogError("Missed {Count} consecutive heartbeats (threshold {Max}). SignalR connection may be stale.",
                    _missedHeartbeats, maxMissed);
            }

            return false;
        }
    }

    public void HandleAcknowledgment(HeartbeatAcknowledgment ack)
    {
        ArgumentNullException.ThrowIfNull(ack);

        lock (_lock)
        {
            _stopwatch.Stop();
            _lastLatencyMs = _stopwatch.Elapsed.TotalMilliseconds;
            _missedHeartbeats = 0;
        }

        _healthService.RecordHeartbeatAcked(_lastLatencyMs);
        _logger.LogDebug("Received HeartbeatAck #{Sequence} (RTT: {Latency:N1}ms)", ack.SequenceNumber, _lastLatencyMs);
    }

    public void ResetMissedCount()
    {
        lock (_lock)
        {
            _missedHeartbeats = 0;
        }
    }
}
