using GamingCafe.ClientAgent.Configuration;
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
}
