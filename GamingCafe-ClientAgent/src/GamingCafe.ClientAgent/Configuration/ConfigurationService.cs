using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Configuration;

public sealed class ConfigurationService : IConfigurationService
{
    private readonly IConfiguration _rawConfig;
    private readonly ILogger<ConfigurationService>? _logger;
    private AgentConfiguration _currentConfig;

    public ConfigurationService(IConfiguration rawConfig, ILogger<ConfigurationService>? logger = null)
    {
        _rawConfig = rawConfig ?? throw new ArgumentNullException(nameof(rawConfig));
        _logger = logger;
        _currentConfig = LoadAndValidate();
    }

    public AgentConfiguration CurrentConfiguration => _currentConfig;

    public void Reload()
    {
        _logger?.LogInformation("Reloading configuration...");
        _currentConfig = LoadAndValidate();
        _logger?.LogInformation("Configuration successfully reloaded.");
    }

    private AgentConfiguration LoadAndValidate()
    {
        var config = new AgentConfiguration();
        _rawConfig.GetSection("Agent").Bind(config.Agent);
        _rawConfig.GetSection("Server").Bind(config.Server);
        _rawConfig.GetSection("Connection").Bind(config.Connection);
        _rawConfig.GetSection("Backend").Bind(config.Backend);
        _rawConfig.GetSection("Reconnection").Bind(config.Reconnection);
        _rawConfig.GetSection("Heartbeat").Bind(config.Heartbeat);
        _rawConfig.GetSection("Identity").Bind(config.Identity);
        _rawConfig.GetSection("Security").Bind(config.Security);
        _rawConfig.GetSection("Logging").Bind(config.Logging);

        // Synchronize Server configuration to Backend URLs if ServerHost / ServerIp is provided
        var host = !string.IsNullOrWhiteSpace(config.Server.ServerIp)
            ? config.Server.ServerIp
            : config.Server.ServerHost;

        if (!string.IsNullOrWhiteSpace(host))
        {
            var httpScheme = config.Server.UseSecureConnection ? "https" : "http";
            var wsScheme = config.Server.UseSecureConnection ? "wss" : "ws";
            var wsPath = config.Server.WebSocketPath.StartsWith("/")
                ? config.Server.WebSocketPath
                : "/" + config.Server.WebSocketPath;

            if (!_rawConfig.GetSection("Backend:ApiBaseUrl").Exists() || string.IsNullOrWhiteSpace(config.Backend.ApiBaseUrl))
            {
                config.Backend.ApiBaseUrl = $"{httpScheme}://{host}:{config.Server.ApiPort}";
            }

            if (!_rawConfig.GetSection("Backend:HubUrl").Exists() || string.IsNullOrWhiteSpace(config.Backend.HubUrl))
            {
                config.Backend.HubUrl = $"{wsScheme}://{host}:{config.Server.WebSocketPort}{wsPath}";
            }
        }
        else
        {
            // Fallback defaults if neither Server nor Backend specified URLs
            if (string.IsNullOrWhiteSpace(config.Backend.ApiBaseUrl))
            {
                config.Backend.ApiBaseUrl = "http://localhost:5022";
            }
            if (string.IsNullOrWhiteSpace(config.Backend.HubUrl))
            {
                config.Backend.HubUrl = "wss://localhost:5001/hubs/agent";
            }
        }

        // Synchronize Connection alias properties if set in centralized config
        if (config.Connection.EnableSignalR.HasValue)
        {
            config.Backend.EnableSignalR = config.Connection.EnableSignalR.Value;
        }
        if (config.Connection.HeartbeatIntervalSeconds.HasValue)
        {
            config.Heartbeat.IntervalSeconds = config.Connection.HeartbeatIntervalSeconds.Value;
        }
        if (config.Connection.ConnectionTimeoutSeconds.HasValue)
        {
            config.Backend.ConnectionTimeoutSeconds = config.Connection.ConnectionTimeoutSeconds.Value;
        }
        if (config.Connection.MaximumReconnectDelaySeconds.HasValue)
        {
            config.Reconnection.MaxDelaySeconds = config.Connection.MaximumReconnectDelaySeconds.Value;
        }

        var environmentName = _rawConfig["Agent:Environment"]
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? config.Agent.Environment;

        var (isValid, errors) = ConfigurationValidator.Validate(config, environmentName);
        if (!isValid)
        {
            var errorMessage = $"Configuration validation failed: {string.Join("; ", errors)}";
            _logger?.LogCritical("{ErrorMessage}", errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        return config;
    }
}
