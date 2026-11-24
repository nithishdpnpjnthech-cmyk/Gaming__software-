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
        _rawConfig.GetSection("Backend").Bind(config.Backend);
        _rawConfig.GetSection("Reconnection").Bind(config.Reconnection);
        _rawConfig.GetSection("Heartbeat").Bind(config.Heartbeat);
        _rawConfig.GetSection("Identity").Bind(config.Identity);
        _rawConfig.GetSection("Security").Bind(config.Security);
        _rawConfig.GetSection("Logging").Bind(config.Logging);

        var (isValid, errors) = ConfigurationValidator.Validate(config);
        if (!isValid)
        {
            var errorMessage = $"Configuration validation failed: {string.Join("; ", errors)}";
            _logger?.LogCritical("{ErrorMessage}", errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        return config;
    }
}
