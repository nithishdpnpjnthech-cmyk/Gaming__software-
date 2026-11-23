using Microsoft.Extensions.Options;

namespace GamingCafe.ClientAgent.Configuration;

/// <summary>
/// Startup validator enforcing zero-trust invariants on AgentConfiguration.
/// Fails fast if insecure schemes, invalid ranges, or missing required settings are detected.
/// </summary>
public class ConfigurationValidator : IValidateOptions<AgentConfiguration>
{
    public ValidateOptionsResult Validate(string? name, AgentConfiguration options)
    {
        var failures = new List<string>();

        // 1. REST Base URL Validation
        if (string.IsNullOrWhiteSpace(options.BackendApiBaseUrl))
        {
            failures.Add("BackendApiBaseUrl cannot be empty.");
        }
        else if (!Uri.TryCreate(options.BackendApiBaseUrl, UriKind.Absolute, out var apiUri))
        {
            failures.Add($"BackendApiBaseUrl '{options.BackendApiBaseUrl}' is not a valid absolute URI.");
        }
        else
        {
            var isLocalhost = apiUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                              apiUri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);

            if (apiUri.Scheme != Uri.UriSchemeHttps && !(options.EnvironmentName.Equals("Development", StringComparison.OrdinalIgnoreCase) && isLocalhost))
            {
                failures.Add($"BackendApiBaseUrl must use HTTPS scheme in {options.EnvironmentName} environment. Provided: {apiUri.Scheme}");
            }
        }

        // 2. SignalR Hub URL Validation
        if (string.IsNullOrWhiteSpace(options.SignalRHubUrl))
        {
            failures.Add("SignalRHubUrl cannot be empty.");
        }
        else if (!Uri.TryCreate(options.SignalRHubUrl, UriKind.Absolute, out var hubUri))
        {
            failures.Add($"SignalRHubUrl '{options.SignalRHubUrl}' is not a valid absolute URI.");
        }
        else
        {
            var isLocalhost = hubUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                              hubUri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);

            var isSecureScheme = hubUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                                 hubUri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase);

            if (!isSecureScheme && !(options.EnvironmentName.Equals("Development", StringComparison.OrdinalIgnoreCase) && isLocalhost))
            {
                failures.Add($"SignalRHubUrl must use WSS or HTTPS scheme in {options.EnvironmentName} environment. Insecure ws:// or http:// is disallowed.");
            }
        }

        // 3. Timeout and interval validations
        if (options.HeartbeatIntervalSeconds < 5)
        {
            failures.Add("HeartbeatIntervalSeconds must be at least 5 seconds.");
        }

        if (options.HttpTimeoutSeconds < 2)
        {
            failures.Add("HttpTimeoutSeconds must be at least 2 seconds.");
        }

        if (options.SignalRConnectionTimeoutSeconds < 5)
        {
            failures.Add("SignalRConnectionTimeoutSeconds must be at least 5 seconds.");
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
