namespace GamingCafe.ClientAgent.Configuration;

public static class ConfigurationValidator
{
    public static (bool IsValid, List<string> Errors) Validate(AgentConfiguration config, string? environmentName = null)
    {
        var errors = new List<string>();

        if (config == null)
        {
            errors.Add("Configuration cannot be null.");
            return (false, errors);
        }

        // Determine environment: parameter overrides explicit config, or falls back to system env / Agent.Environment
        var env = !string.IsNullOrWhiteSpace(environmentName)
            ? environmentName
            : Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
              ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
              ?? config.Agent.Environment;
        bool isDevelopment = string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase);

        // Server validation (if host/ip is provided)
        var serverHost = !string.IsNullOrWhiteSpace(config.Server.ServerIp)
            ? config.Server.ServerIp
            : config.Server.ServerHost;

        if (!string.IsNullOrWhiteSpace(serverHost))
        {
            if (config.Server.ApiPort <= 0 || config.Server.ApiPort > 65535)
            {
                errors.Add($"Invalid Server.ApiPort: '{config.Server.ApiPort}'. Must be between 1 and 65535.");
            }

            if (config.Server.WebSocketPort <= 0 || config.Server.WebSocketPort > 65535)
            {
                errors.Add($"Invalid Server.WebSocketPort: '{config.Server.WebSocketPort}'. Must be between 1 and 65535.");
            }

            if (string.IsNullOrWhiteSpace(config.Server.WebSocketPath))
            {
                errors.Add("Server.WebSocketPath cannot be empty.");
            }
        }

        // Backend validation
        if (!string.IsNullOrWhiteSpace(config.Backend.ApiBaseUrl))
        {
            if (!Uri.TryCreate(config.Backend.ApiBaseUrl, UriKind.Absolute, out var apiUri))
            {
                errors.Add($"Invalid ApiBaseUrl format: '{config.Backend.ApiBaseUrl}'.");
            }
            else
            {
                var apiScheme = apiUri.Scheme.ToLowerInvariant();
                if (config.Security.RejectNonWss && apiScheme != "https" && apiScheme != "http")
                {
                    errors.Add($"Unsupported ApiBaseUrl scheme '{apiScheme}'.");
                }
            }
        }

        if (config.Backend.HttpTimeoutSeconds <= 0)
        {
            errors.Add("HttpTimeoutSeconds must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(config.Backend.HubUrl))
        {
            errors.Add("Backend HubUrl cannot be empty.");
        }
        else if (!Uri.TryCreate(config.Backend.HubUrl, UriKind.Absolute, out var uri))
        {
            errors.Add($"Invalid HubUrl format: '{config.Backend.HubUrl}'.");
        }
        else
        {
            var scheme = uri.Scheme.ToLowerInvariant();
            if (config.Backend.RequireWss || config.Security.RejectNonWss)
            {
                if (scheme != "wss" && scheme != "https")
                {
                    errors.Add($"Insecure HubUrl scheme '{scheme}'. Secure WebSocket requires 'wss://' or 'https://'.");
                }

                if (!config.Backend.ValidateCertificates && string.IsNullOrWhiteSpace(config.Backend.ExpectedCertificateThumbprint))
                {
                    if (!isDevelopment)
                    {
                        errors.Add("Insecure configuration: TLS Certificate validation cannot be disabled in Production environment.");
                    }
                }
            }
            else
            {
                if (!isDevelopment)
                {
                    errors.Add("Insecure configuration: RequireWss must be enabled in Production environment.");
                }

                if (scheme != "wss" && scheme != "https" && scheme != "ws" && scheme != "http")
                {
                    errors.Add($"Unsupported HubUrl scheme '{scheme}'.");
                }
            }
        }

        if (config.Backend.ConnectionTimeoutSeconds <= 0)
        {
            errors.Add("ConnectionTimeoutSeconds must be greater than 0.");
        }

        if (config.Backend.HandshakeTimeoutSeconds <= 0)
        {
            errors.Add("HandshakeTimeoutSeconds must be greater than 0.");
        }

        // Heartbeat validation
        if (config.Heartbeat.IntervalSeconds < 1 || config.Heartbeat.IntervalSeconds > 300)
        {
            errors.Add("Heartbeat IntervalSeconds must be between 1 and 300 seconds.");
        }

        if (config.Heartbeat.TimeoutSeconds < 1 || config.Heartbeat.TimeoutSeconds >= config.Heartbeat.IntervalSeconds * 3)
        {
            errors.Add("Heartbeat TimeoutSeconds must be positive and reasonable relative to interval.");
        }

        if (config.Heartbeat.MaxMissedHeartbeats < 1 || config.Heartbeat.MaxMissedHeartbeats > 10)
        {
            errors.Add("Heartbeat MaxMissedHeartbeats must be between 1 and 10.");
        }

        // Reconnection validation
        if (config.Reconnection.InitialDelaySeconds <= 0)
        {
            errors.Add("InitialDelaySeconds must be greater than 0.");
        }

        if (config.Reconnection.MaxDelaySeconds < config.Reconnection.InitialDelaySeconds)
        {
            errors.Add("MaxDelaySeconds cannot be less than InitialDelaySeconds.");
        }

        if (config.Reconnection.BackoffMultiplier <= 1.0)
        {
            errors.Add("BackoffMultiplier must be greater than 1.0.");
        }

        if (config.Reconnection.JitterMaxSeconds < 0)
        {
            errors.Add("JitterMaxSeconds cannot be negative.");
        }

        // Logging validation
        if (config.Logging.File.MaxFileSizeMb < 1 || config.Logging.File.MaxFileSizeMb > 500)
        {
            errors.Add("File logging MaxFileSizeMb must be between 1 and 500 MB.");
        }

        if (config.Logging.File.RetainedFileDays < 1 || config.Logging.File.RetainedFileDays > 365)
        {
            errors.Add("File logging RetainedFileDays must be between 1 and 365 days.");
        }

        return (errors.Count == 0, errors);
    }
}
