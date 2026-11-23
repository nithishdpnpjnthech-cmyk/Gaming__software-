using Microsoft.Extensions.Options;

namespace GamingCafe.ClientAgent.Configuration;

/// <summary>
/// Startup validator enforcing zero-trust configuration invariants.
/// </summary>
public class AgentOptionsValidator : IValidateOptions<AgentOptions>
{
    public ValidateOptionsResult Validate(string? name, AgentOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.AdminApiBaseUrl))
        {
            failures.Add("AdminApiBaseUrl cannot be empty.");
        }
        else if (!Uri.TryCreate(options.AdminApiBaseUrl, UriKind.Absolute, out var uri))
        {
            failures.Add($"AdminApiBaseUrl '{options.AdminApiBaseUrl}' is not a valid absolute URI.");
        }
        else
        {
            // Enforce HTTPS unless explicitly in Development environment with localhost/loopback
            var isLocalhost = uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                              uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);

            if (uri.Scheme != Uri.UriSchemeHttps && !(options.EnvironmentName.Equals("Development", StringComparison.OrdinalIgnoreCase) && isLocalhost))
            {
                failures.Add($"AdminApiBaseUrl must use HTTPS scheme in {options.EnvironmentName} environment. Provided: {uri.Scheme}");
            }
        }

        if (options.HeartbeatIntervalSeconds < 5)
        {
            failures.Add("HeartbeatIntervalSeconds must be at least 5 seconds.");
        }

        if (options.ConnectionTimeoutSeconds < 2)
        {
            failures.Add("ConnectionTimeoutSeconds must be at least 2 seconds.");
        }

        if (options.MinRetryBackoffSeconds < 1)
        {
            failures.Add("MinRetryBackoffSeconds must be at least 1 second.");
        }

        if (options.MaxRetryBackoffSeconds < options.MinRetryBackoffSeconds)
        {
            failures.Add("MaxRetryBackoffSeconds must be greater than or equal to MinRetryBackoffSeconds.");
        }

        if (options.BackoffJitterFactor < 0.0 || options.BackoffJitterFactor > 0.5)
        {
            failures.Add("BackoffJitterFactor must be between 0.0 and 0.5.");
        }

        if (options.TlsValidationMode.Equals("PinnedThumbprint", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(options.PinnedCertificateThumbprint))
        {
            failures.Add("PinnedCertificateThumbprint is required when TlsValidationMode is 'PinnedThumbprint'.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
