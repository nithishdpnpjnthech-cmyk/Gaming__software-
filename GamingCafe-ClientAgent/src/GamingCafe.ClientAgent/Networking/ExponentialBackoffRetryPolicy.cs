using System.Security.Cryptography;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Interfaces;
using Microsoft.Extensions.Options;

namespace GamingCafe.ClientAgent.Networking;

/// <summary>
/// Implements exponential backoff with full jitter to avoid thundering-herd issues
/// when multiple Gaming PCs reconnect simultaneously after a network or power interruption.
/// </summary>
public class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    private readonly AgentOptions _options;

    public ExponentialBackoffRetryPolicy(IOptions<AgentOptions> options)
    {
        _options = options.Value;
    }

    public TimeSpan GetNextDelay(int attemptCount)
    {
        var minSec = _options.MinRetryBackoffSeconds;
        var maxSec = _options.MaxRetryBackoffSeconds;
        var jitterFactor = _options.BackoffJitterFactor;

        // Base exponential: minSec * 2^(attempt - 1)
        var power = Math.Min(attemptCount - 1, 10);
        var baseSeconds = minSec * Math.Pow(2, Math.Max(0, power));
        var cappedSeconds = Math.Min(baseSeconds, maxSec);

        // Apply cryptographic jitter in range: cappedSeconds * (1 - jitter) to cappedSeconds * (1 + jitter)
        var randomDouble = RandomNumberGenerator.GetInt32(0, 10000) / 10000.0;
        var jitterMultiplier = 1.0 - jitterFactor + (2.0 * jitterFactor * randomDouble);
        var finalSeconds = Math.Clamp(cappedSeconds * jitterMultiplier, minSec, maxSec);

        return TimeSpan.FromSeconds(finalSeconds);
    }

    public void Reset()
    {
        // Stateless calculation based on passed attempt count; reset hook for callers
    }
}
