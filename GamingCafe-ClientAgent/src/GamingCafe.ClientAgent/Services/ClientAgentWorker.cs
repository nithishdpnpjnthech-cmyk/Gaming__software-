using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Interfaces;
using GamingCafe.ClientAgent.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GamingCafe.ClientAgent.Services;

/// <summary>
/// Core background worker orchestrating device identity, enrollment lifecycle, and heartbeat loops.
/// Runs as a resilient Windows Service without requiring an active user session.
/// </summary>
public class ClientAgentWorker : BackgroundService
{
    private readonly IDeviceIdentityService _identityService;
    private readonly IEnrollmentService _enrollmentService;
    private readonly IHeartbeatService _heartbeatService;
    private readonly IRetryPolicy _retryPolicy;
    private readonly IHealthTracker _healthTracker;
    private readonly IOptions<AgentOptions> _options;
    private readonly ILogger<ClientAgentWorker> _logger;

    private int _consecutiveNetworkErrors;

    public ClientAgentWorker(
        IDeviceIdentityService identityService,
        IEnrollmentService enrollmentService,
        IHeartbeatService heartbeatService,
        IRetryPolicy retryPolicy,
        IHealthTracker healthTracker,
        IOptions<AgentOptions> options,
        ILogger<ClientAgentWorker> logger)
    {
        _identityService = identityService;
        _enrollmentService = enrollmentService;
        _heartbeatService = heartbeatService;
        _retryPolicy = retryPolicy;
        _healthTracker = healthTracker;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Gaming Cafe Client Agent Worker starting. Agent Version: {Version}, Target: {Url}",
            _options.Value.AgentVersion, _options.Value.AdminApiBaseUrl);

        _healthTracker.RecordServiceStarted();

        DeviceIdentity identity;
        try
        {
            identity = await _identityService.GetOrCreateIdentityAsync(stoppingToken);
            _logger.LogInformation("Agent initialized with Device ID: {DeviceId} for Host: {Host}",
                identity.DeviceId, identity.Hostname);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Agent startup cancelled during identity initialization.");
            return;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error initializing device identity. Agent cannot proceed.");
            throw;
        }

        // Determine starting enrollment state
        await _enrollmentService.EvaluateInitialStateAsync(identity, stoppingToken);

        // Main lifecycle loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var state = _enrollmentService.CurrentState;

                switch (state)
                {
                    case EnrollmentState.Unregistered:
                    case EnrollmentState.EnrollmentPending:
                    case EnrollmentState.Approved:
                    case EnrollmentState.ErrorRetrying:
                        await HandleEnrollmentPhaseAsync(identity, stoppingToken);
                        break;

                    case EnrollmentState.Authenticated:
                        await HandleHeartbeatPhaseAsync(identity, stoppingToken);
                        break;

                    case EnrollmentState.Rejected:
                        _logger.LogWarning("Device enrollment is REJECTED by administrator. Waiting before re-checking.");
                        await DelaySafelyAsync(TimeSpan.FromSeconds(Math.Max(_options.Value.MaxRetryBackoffSeconds, 60)), stoppingToken);
                        await _enrollmentService.ProcessEnrollmentStepAsync(identity, stoppingToken);
                        break;

                    case EnrollmentState.Revoked:
                        _logger.LogWarning("Device authorization is REVOKED. Normal agent operations are suspended.");
                        await DelaySafelyAsync(TimeSpan.FromSeconds(Math.Max(_options.Value.MaxRetryBackoffSeconds, 60)), stoppingToken);
                        break;

                    default:
                        _logger.LogWarning("Unknown state {State}. Pausing briefly.", state);
                        await DelaySafelyAsync(TimeSpan.FromSeconds(5), stoppingToken);
                        break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in agent main loop. Applying backoff recovery.");
                _consecutiveNetworkErrors++;
                var backoff = _retryPolicy.GetNextDelay(_consecutiveNetworkErrors);
                await DelaySafelyAsync(backoff, stoppingToken);
            }
        }

        _logger.LogInformation("Gaming Cafe Client Agent Worker shutdown complete.");
    }

    private async Task HandleEnrollmentPhaseAsync(DeviceIdentity identity, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Executing enrollment step. Current state: [{State}]", _enrollmentService.CurrentState);

        var newState = await _enrollmentService.ProcessEnrollmentStepAsync(identity, stoppingToken);

        if (newState == EnrollmentState.Authenticated)
        {
            _logger.LogInformation("Device successfully transitioned to Authenticated! Commencing heartbeat loop.");
            _retryPolicy.Reset();
            _consecutiveNetworkErrors = 0;
            return;
        }

        if (newState == EnrollmentState.EnrollmentPending)
        {
            var pollInterval = TimeSpan.FromSeconds(_options.Value.EnrollmentPollIntervalSeconds);
            _logger.LogInformation("Device pending approval. Polling again in {Seconds}s.", pollInterval.TotalSeconds);
            await DelaySafelyAsync(pollInterval, stoppingToken);
            return;
        }

        if (newState == EnrollmentState.ErrorRetrying)
        {
            _consecutiveNetworkErrors++;
            var delay = _retryPolicy.GetNextDelay(_consecutiveNetworkErrors);
            _logger.LogWarning("Enrollment communication encountered an issue. Retrying (attempt {Attempt}) in {Seconds:F1}s.",
                _consecutiveNetworkErrors, delay.TotalSeconds);
            await DelaySafelyAsync(delay, stoppingToken);
            return;
        }

        // Otherwise small delay to prevent rapid looping
        await DelaySafelyAsync(TimeSpan.FromSeconds(5), stoppingToken);
    }

    private async Task HandleHeartbeatPhaseAsync(DeviceIdentity identity, CancellationToken stoppingToken)
    {
        var success = await _heartbeatService.SendHeartbeatOnceAsync(identity, stoppingToken);

        if (success)
        {
            _retryPolicy.Reset();
            _consecutiveNetworkErrors = 0;

            var interval = TimeSpan.FromSeconds(_options.Value.HeartbeatIntervalSeconds);
            _logger.LogDebug("Heartbeat successful. Next heartbeat in {Seconds}s.", interval.TotalSeconds);
            await DelaySafelyAsync(interval, stoppingToken);
        }
        else
        {
            _consecutiveNetworkErrors++;
            var delay = _retryPolicy.GetNextDelay(_consecutiveNetworkErrors);

            _logger.LogWarning("Heartbeat unsuccessful (attempt {Attempt}). Retrying with backoff in {Seconds:F1}s.",
                _consecutiveNetworkErrors, delay.TotalSeconds);

            // If consecutive failures indicate possible token revocation or expiry, re-evaluate state
            if (_consecutiveNetworkErrors >= 3)
            {
                _logger.LogInformation("Multiple heartbeat failures detected. Re-evaluating enrollment state with backend.");
                await _enrollmentService.ProcessEnrollmentStepAsync(identity, stoppingToken);
            }

            await DelaySafelyAsync(delay, stoppingToken);
        }
    }

    private static async Task DelaySafelyAsync(TimeSpan delay, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(delay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on service shutdown
        }
    }
}
