using System.Security.Cryptography;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Enrollment;
using GamingCafe.ClientAgent.Health;
using GamingCafe.ClientAgent.Heartbeat;
using GamingCafe.ClientAgent.Identity;
using GamingCafe.ClientAgent.Realtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GamingCafe.ClientAgent;

/// <summary>
/// Core background service executing as a Windows Service.
/// Orchestrates device identity, enrollment, real-time SignalR connectivity, and periodic heartbeats.
/// </summary>
public class Worker : BackgroundService
{
    private readonly IDeviceIdentityService _identityService;
    private readonly IEnrollmentService _enrollmentService;
    private readonly ISignalRConnectionManager _signalRManager;
    private readonly IHeartbeatService _heartbeatService;
    private readonly IAgentHealthService _healthService;
    private readonly IOptions<AgentConfiguration> _options;
    private readonly ILogger<Worker> _logger;

    private int _consecutiveErrors;

    public Worker(
        IDeviceIdentityService identityService,
        IEnrollmentService enrollmentService,
        ISignalRConnectionManager signalRManager,
        IHeartbeatService heartbeatService,
        IAgentHealthService healthService,
        IOptions<AgentConfiguration> options,
        ILogger<Worker> logger)
    {
        _identityService = identityService;
        _enrollmentService = enrollmentService;
        _signalRManager = signalRManager;
        _heartbeatService = heartbeatService;
        _healthService = healthService;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Gaming Cafe Client Agent starting. Version: {Version}, Service: Windows Service Host",
            _options.Value.AgentVersion);

        _healthService.RecordServiceStarted();

        // 1. Initialize or load persistent Device Identity
        DeviceIdentity identity;
        try
        {
            identity = await _identityService.GetOrCreateIdentityAsync(stoppingToken);
            _healthService.RecordDeviceId(identity.DeviceId);
            _logger.LogInformation("Device Identity verified. Device ID: {DeviceId}, Host: {Host}",
                identity.DeviceId, identity.Hostname);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal failure during device identity initialization.");
            throw;
        }

        // 2. Hook real-time SignalR events
        _signalRManager.StateChanged += state => _healthService.RecordSignalRState(state);
        _signalRManager.EnrollmentStatusChanged += async evt =>
        {
            if (string.Equals(evt.NewStatus, "Revoked", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Real-time revocation event received from server.");
                await _enrollmentService.HandleRevocationAsync(stoppingToken);
                await _signalRManager.StopAsync(stoppingToken);
            }
        };

        // 3. Determine starting enrollment state
        await _enrollmentService.EvaluateInitialStateAsync(identity, stoppingToken);

        // 4. Main Agent Lifecycle Loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var currentState = _enrollmentService.CurrentState;
                _healthService.RecordEnrollmentState(currentState);

                switch (currentState)
                {
                    case EnrollmentState.Unregistered:
                    case EnrollmentState.RegistrationPending:
                    case EnrollmentState.PendingApproval:
                    case EnrollmentState.Approved:
                    case EnrollmentState.Error:
                    case EnrollmentState.Retrying:
                        await HandleEnrollmentPhaseAsync(identity, stoppingToken);
                        break;

                    case EnrollmentState.Authenticated:
                        await HandleAuthenticatedPhaseAsync(identity, stoppingToken);
                        break;

                    case EnrollmentState.Rejected:
                        _logger.LogWarning("Device enrollment was REJECTED. Pausing before re-checking.");
                        await _signalRManager.StopAsync(stoppingToken);
                        await DelayWithCancellationAsync(TimeSpan.FromSeconds(Math.Max(_options.Value.MaxRetryBackoffSeconds, 60)), stoppingToken);
                        await _enrollmentService.ProcessEnrollmentStepAsync(identity, stoppingToken);
                        break;

                    case EnrollmentState.Revoked:
                        _logger.LogWarning("Device authorization is REVOKED. Client operations suspended.");
                        await _signalRManager.StopAsync(stoppingToken);
                        await DelayWithCancellationAsync(TimeSpan.FromSeconds(Math.Max(_options.Value.MaxRetryBackoffSeconds, 60)), stoppingToken);
                        break;

                    default:
                        await DelayWithCancellationAsync(TimeSpan.FromSeconds(5), stoppingToken);
                        break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                _healthService.RecordApiFailure(ex.Message);
                var backoff = ComputeBackoffDelay(_consecutiveErrors);
                _logger.LogError(ex, "Unexpected exception in main loop (Failure {Count}). Backing off for {Sec:F1}s.",
                    _consecutiveErrors, backoff.TotalSeconds);
                await DelayWithCancellationAsync(backoff, stoppingToken);
            }
        }

        // 5. Graceful shutdown cleanup
        _logger.LogInformation("Agent shutdown signal received. Disconnecting SignalR and stopping services...");
        await _signalRManager.StopAsync(CancellationToken.None);
        _logger.LogInformation("Gaming Cafe Client Agent stopped gracefully.");
    }

    private async Task HandleEnrollmentPhaseAsync(DeviceIdentity identity, CancellationToken stoppingToken)
    {
        var newState = await _enrollmentService.ProcessEnrollmentStepAsync(identity, stoppingToken);
        _healthService.RecordEnrollmentState(newState);

        if (newState == EnrollmentState.Authenticated)
        {
            _logger.LogInformation("Device transitioned to Authenticated! Commencing real-time communication.");
            _consecutiveErrors = 0;
            return;
        }

        if (newState == EnrollmentState.PendingApproval || newState == EnrollmentState.RegistrationPending)
        {
            var pollInterval = TimeSpan.FromSeconds(_options.Value.EnrollmentPollIntervalSeconds);
            _logger.LogInformation("Device is Pending Approval. Polling status again in {Sec}s.", pollInterval.TotalSeconds);
            await DelayWithCancellationAsync(pollInterval, stoppingToken);
            return;
        }

        if (newState == EnrollmentState.Error || newState == EnrollmentState.Retrying)
        {
            _consecutiveErrors++;
            var backoff = ComputeBackoffDelay(_consecutiveErrors);
            _logger.LogWarning("Enrollment communication failure. Retrying in {Sec:F1}s.", backoff.TotalSeconds);
            await DelayWithCancellationAsync(backoff, stoppingToken);
            return;
        }

        await DelayWithCancellationAsync(TimeSpan.FromSeconds(5), stoppingToken);
    }

    private async Task HandleAuthenticatedPhaseAsync(DeviceIdentity identity, CancellationToken stoppingToken)
    {
        // Ensure SignalR WSS connection is active
        if (_signalRManager.CurrentState == RealtimeConnectionState.Disconnected ||
            _signalRManager.CurrentState == RealtimeConnectionState.Failed)
        {
            _logger.LogInformation("Starting SignalR real-time connection...");
            await _signalRManager.StartAsync(stoppingToken);
        }

        // Send Heartbeat (HeartbeatService tries SignalR first, falls back to REST automatically)
        var success = await _heartbeatService.SendHeartbeatAsync(identity, stoppingToken);

        if (success)
        {
            _consecutiveErrors = 0;
            var interval = TimeSpan.FromSeconds(_options.Value.HeartbeatIntervalSeconds);
            await DelayWithCancellationAsync(interval, stoppingToken);
        }
        else
        {
            _consecutiveErrors++;
            var delay = ComputeBackoffDelay(_consecutiveErrors);
            _logger.LogWarning("Heartbeat dispatch unsuccessful. Retrying in {Sec:F1}s.", delay.TotalSeconds);

            if (_consecutiveErrors >= 3)
            {
                _logger.LogInformation("Multiple heartbeat failures detected. Re-evaluating backend enrollment status.");
                await _enrollmentService.ProcessEnrollmentStepAsync(identity, stoppingToken);
            }

            await DelayWithCancellationAsync(delay, stoppingToken);
        }
    }

    private TimeSpan ComputeBackoffDelay(int attemptCount)
    {
        var minSec = _options.Value.MinRetryBackoffSeconds;
        var maxSec = _options.Value.MaxRetryBackoffSeconds;
        var jitterFactor = _options.Value.BackoffJitterFactor;

        var power = Math.Min(attemptCount - 1, 10);
        var baseSeconds = minSec * Math.Pow(2, Math.Max(0, power));
        var cappedSeconds = Math.Min(baseSeconds, maxSec);

        var randomDouble = RandomNumberGenerator.GetInt32(0, 10000) / 10000.0;
        var multiplier = 1.0 - jitterFactor + (2.0 * jitterFactor * randomDouble);
        var finalSeconds = Math.Clamp(cappedSeconds * multiplier, minSec, maxSec);

        return TimeSpan.FromSeconds(finalSeconds);
    }

    private static async Task DelayWithCancellationAsync(TimeSpan delay, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(delay, stoppingToken);
        }
        catch (OperationCanceledException) { }
    }
}
