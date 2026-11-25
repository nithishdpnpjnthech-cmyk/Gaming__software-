using System.Collections.Concurrent;
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
    private readonly object _lock = new();

    private readonly ConcurrentDictionary<long, InflightHeartbeat> _inflightHeartbeats = new();

    private sealed class InflightHeartbeat
    {
        public required long SequenceNumber { get; init; }
        public required string MessageId { get; init; }
        public required Stopwatch Stopwatch { get; init; }
        public required TaskCompletionSource<HeartbeatAcknowledgment> Tcs { get; init; }
        public DateTimeOffset SentAtUtc { get; init; } = DateTimeOffset.UtcNow;
    }

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
    public int MissedHeartbeats => Volatile.Read(ref _missedHeartbeats);
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

        // Use Backend-generated DeviceId if available, fallback to client identity DeviceId
        var targetDeviceId = identity.BackendDeviceId ?? identity.DeviceId;

        var message = new HeartbeatMessage
        {
            DeviceId = targetDeviceId,
            SequenceNumber = seq,
            AgentVersion = identity.ClientVersion,
            UpTimeSeconds = metrics.UptimeSeconds,
            CpuPercent = metrics.CpuPercent,
            MemoryUsageMb = metrics.MemoryUsageMb,
            TimestampUtc = DateTimeOffset.UtcNow,
            ConnectionStatus = _connectionManager.IsConnected ? AgentConnectionStatus.Connected : AgentConnectionStatus.Disconnected
        };

        var stopwatch = Stopwatch.StartNew();
        var tcs = new TaskCompletionSource<HeartbeatAcknowledgment>(TaskCreationOptions.RunContinuationsAsynchronously);
        var inflight = new InflightHeartbeat
        {
            SequenceNumber = seq,
            MessageId = message.MessageId,
            Stopwatch = stopwatch,
            Tcs = tcs
        };

        _inflightHeartbeats[seq] = inflight;

        try
        {
            _logger.LogDebug("Dispatching SignalR heartbeat #{Sequence} (DeviceId: {DeviceId}, Uptime: {Uptime}s, Cpu: {Cpu:N1}%, Mem: {Mem:N1}MB)",
                seq, targetDeviceId, metrics.UptimeSeconds, metrics.CpuPercent, metrics.MemoryUsageMb);

            await _connectionManager.SendAsync("SendHeartbeat", message, cancellationToken).ConfigureAwait(false);
            _healthService.RecordHeartbeatSent();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _inflightHeartbeats.TryRemove(seq, out _);
            _logger.LogDebug("Heartbeat #{Sequence} dispatch cancelled due to service shutdown.", seq);
            return false;
        }
        catch (Exception ex)
        {
            _inflightHeartbeats.TryRemove(seq, out _);
            var missed = Interlocked.Increment(ref _missedHeartbeats);
            _logger.LogWarning("Failed to dispatch SignalR heartbeat #{Sequence}: {Reason}. Consecutive missed count: {MissedCount}",
                seq, ex.Message, missed);
            _logger.LogDebug(ex, "Detailed exception for failed heartbeat #{Sequence}:", seq);

            CheckMissedThreshold(missed);
            return false;
        }

        // Server acknowledgement tracking:
        // Do not consider a heartbeat successful merely because it was sent. Track server acknowledgements.
        var timeoutSeconds = Math.Max(1, _configService.CurrentConfiguration.Heartbeat.TimeoutSeconds);
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            var ack = await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            stopwatch.Stop();
            var latencyMs = stopwatch.Elapsed.TotalMilliseconds;

            lock (_lock)
            {
                _lastLatencyMs = latencyMs;
            }

            Interlocked.Exchange(ref _missedHeartbeats, 0);
            _healthService.RecordHeartbeatAcked(latencyMs);
            _logger.LogDebug("Heartbeat #{Sequence} successfully acknowledged by server (RTT: {Latency:N1}ms)",
                seq, latencyMs);

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _inflightHeartbeats.TryRemove(seq, out _);
            _logger.LogDebug("Heartbeat #{Sequence} acknowledgment wait cancelled during shutdown.", seq);
            return false;
        }
        catch (Exception)
        {
            _inflightHeartbeats.TryRemove(seq, out _);
            var missed = Interlocked.Increment(ref _missedHeartbeats);
            _logger.LogWarning("Heartbeat #{Sequence} timed out after {Timeout}s waiting for server acknowledgment. Consecutive missed count: {MissedCount}",
                seq, timeout.TotalSeconds, missed);

            CheckMissedThreshold(missed);
            return false;
        }
    }

    public void HandleAcknowledgment(HeartbeatAcknowledgment ack)
    {
        ArgumentNullException.ThrowIfNull(ack);

        if (_inflightHeartbeats.TryRemove(ack.SequenceNumber, out var inflight))
        {
            inflight.Stopwatch.Stop();
            inflight.Tcs.TrySetResult(ack);
        }
        else
        {
            // Late or duplicate acknowledgment
            _logger.LogDebug("Received HeartbeatAck #{Sequence} (no inflight waiter found or already completed)", ack.SequenceNumber);
        }

        lock (_lock)
        {
            _missedHeartbeats = 0;
        }
    }

    public void ResetMissedCount()
    {
        Interlocked.Exchange(ref _missedHeartbeats, 0);
    }

    private void CheckMissedThreshold(int missed)
    {
        var maxMissed = _configService.CurrentConfiguration.Heartbeat.MaxMissedHeartbeats;
        if (missed >= maxMissed)
        {
            _logger.LogError("Missed {Count} consecutive heartbeats (threshold {Max}). SignalR connection is unresponsive. Triggering connection recovery.",
                missed, maxMissed);

            _ = Task.Run(async () =>
            {
                try
                {
                    _logger.LogWarning("Initiating connection recovery due to missed heartbeat threshold...");
                    await _connectionManager.StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception recoveryEx)
                {
                    _logger.LogDebug(recoveryEx, "Exception during connection recovery triggered by missed heartbeats.");
                }
            });
        }
    }
}
