using GamingCafe.ClientAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GamingCafe.ClientAgent.Tests;

public class ConfigurationValidationTests
{
    [Fact]
    public void DefaultConfiguration_ShouldBeValid()
    {
        var config = new AgentConfiguration();
        var (isValid, errors) = ConfigurationValidator.Validate(config);

        Assert.True(isValid, $"Default config should be valid. Errors: {string.Join(", ", errors)}");
    }

    [Theory]
    [InlineData("ws://localhost:5001/hubs/agent")]
    [InlineData("http://localhost:5000/hubs/agent")]
    [InlineData("ftp://localhost/hubs")]
    public void InsecureScheme_WithRequireWssTrue_ShouldFailValidation(string invalidUrl)
    {
        var config = new AgentConfiguration();
        config.Backend.HubUrl = invalidUrl;
        config.Backend.RequireWss = true;

        var (isValid, errors) = ConfigurationValidator.Validate(config);

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("Insecure HubUrl scheme"));
    }

    [Theory]
    [InlineData("wss://localhost:5001/hubs/agent")]
    [InlineData("https://backend.gamingcafe.local/hubs/agent")]
    public void SecureWssScheme_ShouldPassValidation(string secureUrl)
    {
        var config = new AgentConfiguration();
        config.Backend.HubUrl = secureUrl;
        config.Backend.RequireWss = true;

        var (isValid, errors) = ConfigurationValidator.Validate(config);

        Assert.True(isValid, $"Config should be valid. Errors: {string.Join(", ", errors)}");
    }

    [Fact]
    public void InvalidHeartbeatIntervals_ShouldFailValidation()
    {
        var config = new AgentConfiguration();
        config.Heartbeat.IntervalSeconds = 0;

        var (isValid, errors) = ConfigurationValidator.Validate(config);

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("IntervalSeconds must be between 1 and 300"));
    }

    [Fact]
    public void MaxDelayLessThanInitialDelay_ShouldFailValidation()
    {
        var config = new AgentConfiguration();
        config.Reconnection.InitialDelaySeconds = 10;
        config.Reconnection.MaxDelaySeconds = 5;

        var (isValid, errors) = ConfigurationValidator.Validate(config);

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("MaxDelaySeconds cannot be less than InitialDelaySeconds"));
    }

    [Fact]
    public void ServerIp_AutomaticallyBuilds_BackendUrls()
    {
        var rawConfigData = new Dictionary<string, string?>
        {
            ["Server:ServerIp"] = "192.168.1.150",
            ["Server:ApiPort"] = "5022",
            ["Server:WebSocketPort"] = "5001",
            ["Server:WebSocketPath"] = "/hubs/agent",
            ["Server:UseSecureConnection"] = "true"
        };

        var memConfig = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(rawConfigData)
            .Build();

        var configService = new ConfigurationService(memConfig);
        var config = configService.CurrentConfiguration;

        Assert.Equal("https://192.168.1.150:5022", config.Backend.ApiBaseUrl);
        Assert.Equal("wss://192.168.1.150:5001/hubs/agent", config.Backend.HubUrl);
    }

    [Fact]
    public void ServerConfig_WithInvalidPort_FailsValidation()
    {
        var config = new AgentConfiguration();
        config.Server.ServerIp = "192.168.1.100";
        config.Server.ApiPort = 70000; // Out of range

        var (isValid, errors) = ConfigurationValidator.Validate(config);

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("Invalid Server.ApiPort"));
    }

    [Fact]
    public void Production_WhenValidateCertificatesFalse_FailsValidation()
    {
        var config = new AgentConfiguration();
        config.Agent.Environment = "Production";
        config.Backend.ValidateCertificates = false;
        config.Backend.ExpectedCertificateThumbprint = "";

        var (isValid, errors) = ConfigurationValidator.Validate(config, "Production");

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("TLS Certificate validation cannot be disabled in Production environment"));
    }

    [Fact]
    public void Development_WhenValidateCertificatesFalse_PassesValidation()
    {
        var config = new AgentConfiguration();
        config.Agent.Environment = "Development";
        config.Backend.ValidateCertificates = false;
        config.Backend.RequireWss = true;
        config.Security.RejectNonWss = true;

        var (isValid, errors) = ConfigurationValidator.Validate(config, "Development");

        Assert.True(isValid, $"Development config should be valid. Errors: {string.Join(", ", errors)}");
    }

    [Fact]
    public void Production_WhenRequireWssFalse_FailsValidation()
    {
        var config = new AgentConfiguration();
        config.Agent.Environment = "Production";
        config.Backend.RequireWss = false;
        config.Security.RejectNonWss = false;

        var (isValid, errors) = ConfigurationValidator.Validate(config, "Production");

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("RequireWss must be enabled in Production environment"));
    }
}
