using GamingCafe.ClientAgent.Authentication;
using GamingCafe.ClientAgent.Communication;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Health;
using GamingCafe.ClientAgent.Identity;
using GamingCafe.ClientAgent.Realtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GamingCafe.ClientAgent.Heartbeat;

/// <summary>
/// Contract for the heartbeat dispatcher.
/// </summary>
public interface IHeartbeatService
{
    Task<bool> SendHeartbeatAsync(DeviceIdentity identity, CancellationToken cancellationToken = default);
}

/// <summary>
/// Dispatches presence heartbeats periodically.
/// Prefers real-time SignalR WebSocket communication; automatically falls back to HTTPS REST
/// when SignalR is disconnected or fails.
/// </summary>
public class HeartbeatService : IHeartbeatService
{
    private readonly ISignalRHubClient _signalRClient;
    private readonly IBackendApiClient _apiClient;
    private readonly IDeviceAuthenticationService _authService;
    private readonly IAgentHealthService _healthService;
    private readonly IOptions<AgentConfiguration> _options;
    private readonly ILogger<HeartbeatService> _logger;
    private readonly SemaphoreSlim _concurrencyGuard = new(1, 1);

    public HeartbeatService(
        ISignalRHubClient signalRClient,
        IBackendApiClient apiClient,
        IDeviceAuthenticationService authService,
        IAgentHealthService healthService,
        IOptions<AgentConfiguration> options,
        ILogger<HeartbeatService> logger)
    {
        _signalRClient = signalRClient;
        _apiClient = apiClient;
        _authService = authService;
        _healthService = healthService;
        _options = options;
        _logger = logger;
    }

    public async Task<bool> SendHeartbeatAsync(DeviceIdentity identity, CancellationToken cancellationToken = default)
    {
        // Concurrency guard: avoid overlapping heartbeat calls
        if (!await _concurrencyGuard.WaitAsync(0, cancellationToken))
        {
            _logger.LogWarning("Heartbeat skipped: previous heartbeat is still in progress.");
            return false;
        }

        try
        {
            var secret = await _authService.GetDeviceSecretAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(secret))
            {
                _logger.LogWarning("Heartbeat aborted: No valid authentication secret available.");
                return false;
            }

            var health = _healthService.GetCurrentHealth();
            var payload = new HeartbeatPayload
            {
                DeviceId = identity.DeviceId,
                Hostname = identity.Hostname,
                AgentVersion = _options.Value.AgentVersion,
                ProtocolVersion = _options.Value.ProtocolVersion,
                TimestampUtc = DateTime.UtcNow,
                AgentStatus = health.AgentRunningState,
                ServiceUptimeSeconds = health.ServiceUptimeSeconds,
                ConnectionTransport = _signalRClient.IsConnected ? "SignalR" : "REST",
                ProcessMemoryBytes = health.ProcessMemoryBytes,
                LastCommunicationUtc = health.LastSuccessfulHeartbeatUtc
            };

            // 1. Primary: Prefer SignalR WSS real-time connection
            if (_signalRClient.IsConnected)
            {
                _logger.LogDebug("Sending heartbeat via primary transport: SignalR WSS");
                var signalRSuccess = await _signalRClient.SendHeartbeatAsync(payload, cancellationToken);
                if (signalRSuccess)
                {
                    _healthService.RecordHeartbeatSuccess();
                    _logger.LogInformation("SignalR heartbeat acknowledged.");
                    return true;
                }
                _logger.LogWarning("SignalR heartbeat failed. Engaging REST fallback...");
            }
            else
            {
                _logger.LogDebug("SignalR not connected. Using REST fallback transport.");
            }

            // 2. Secondary: Fallback to HTTPS REST API
            var restPayload = payload with { ConnectionTransport = "REST" };
            var response = await _apiClient.SendHeartbeatFallbackAsync(restPayload, secret, cancellationToken);

            if (response.IsSuccess && response.Data != null)
            {
                _healthService.RecordHeartbeatSuccess();
                _logger.LogInformation("REST fallback heartbeat acknowledged at {ServerTime} UTC",
                    response.Data.ServerTimestampUtc);
                return true;
            }

            _healthService.RecordApiFailure(response.ErrorMessage ?? "Heartbeat REST fallback failed.");
            _logger.LogWarning("Heartbeat delivery failed on both SignalR and REST ({StatusCode}): {Error}",
                response.StatusCode, response.ErrorMessage);

            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Heartbeat cancelled due to service shutdown.");
            return false;
        }
        catch (Exception ex)
        {
            _healthService.RecordApiFailure(ex.Message);
            _logger.LogError(ex, "Unexpected error during heartbeat transmission.");
            return false;
        }
        finally
        {
            _concurrencyGuard.Release();
        }
    }
}
