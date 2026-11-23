using System.Security.Cryptography;
using GamingCafe.ClientAgent.Configuration;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Communication;

public interface IReconnectionService : IRetryPolicy
{
    TimeSpan CalculateNextDelay(int retryAttempt);
}

public sealed class ReconnectionService : IReconnectionService
{
    private readonly IConfigurationService _configService;
    private readonly ILogger<ReconnectionService> _logger;

    public ReconnectionService(
        IConfigurationService configService,
        ILogger<ReconnectionService> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        var config = _configService.CurrentConfiguration.Reconnection;
        var hubUrl = _configService.CurrentConfiguration.Backend.HubUrl;
        var attempt = (int)retryContext.PreviousRetryCount + 1;

        if (config.MaxReconnectAttempts > 0 && attempt > config.MaxReconnectAttempts)
        {
            _logger.LogWarning("Reconnection to backend '{HubUrl}' aborted: reached maximum retry attempts ({MaxAttempts}).",
                hubUrl, config.MaxReconnectAttempts);
            return null;
        }

        var delay = CalculateNextDelay(attempt);
        var (category, reason) = ConnectionErrorClassifier.Classify(retryContext.RetryReason);

        _logger.LogWarning("Reconnection to backend '{HubUrl}' scheduled in {Delay:N1}s (attempt {Attempt}) [{Category}]: {Reason}",
            hubUrl, delay.TotalSeconds, attempt, category, reason);

        if (retryContext.RetryReason != null)
        {
            _logger.LogDebug(retryContext.RetryReason, "Detailed reconnection exception for '{HubUrl}':", hubUrl);
        }

        return delay;
    }

    public TimeSpan CalculateNextDelay(int retryAttempt)
    {
        var config = _configService.CurrentConfiguration.Reconnection;
        var exponentialDelay = config.InitialDelaySeconds * Math.Pow(config.BackoffMultiplier, Math.Max(0, retryAttempt - 1));
        var cappedDelay = Math.Min(exponentialDelay, config.MaxDelaySeconds);

        // Add cryptographic jitter [0, JitterMaxSeconds]
        var jitter = (RandomNumberGenerator.GetInt32(0, 1000) / 1000.0) * config.JitterMaxSeconds;
        var totalSeconds = cappedDelay + jitter;

        return TimeSpan.FromSeconds(totalSeconds);
    }
}
