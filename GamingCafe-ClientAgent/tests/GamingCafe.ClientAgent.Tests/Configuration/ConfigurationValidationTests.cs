using GamingCafe.ClientAgent.Configuration;
using Xunit;

namespace GamingCafe.ClientAgent.Tests.Configuration;

public class ConfigurationValidationTests
{
    private readonly ConfigurationValidator _validator = new();

    [Fact]
    public void Validate_ValidProductionOptions_ReturnsSuccess()
    {
        var options = new AgentConfiguration
        {
            BackendApiBaseUrl = "https://admin.cafe.lan:5001",
            SignalRHubUrl = "https://admin.cafe.lan:5001/hubs/agent",
            HeartbeatIntervalSeconds = 30,
            HttpTimeoutSeconds = 10,
            SignalRConnectionTimeoutSeconds = 15,
            MinRetryBackoffSeconds = 2,
            MaxRetryBackoffSeconds = 60,
            BackoffJitterFactor = 0.2,
            EnvironmentName = "Production"
        };

        var result = _validator.Validate(null, options);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_HttpInProduction_ReturnsFailure()
    {
        var options = new AgentConfiguration
        {
            BackendApiBaseUrl = "http://admin.cafe.lan:5001",
            SignalRHubUrl = "https://admin.cafe.lan:5001/hubs/agent",
            EnvironmentName = "Production"
        };

        var result = _validator.Validate(null, options);
        Assert.True(result.Failed);
        Assert.Contains("must use HTTPS", result.FailureMessage);
    }

    [Fact]
    public void Validate_InsecureWsInProduction_ReturnsFailure()
    {
        var options = new AgentConfiguration
        {
            BackendApiBaseUrl = "https://admin.cafe.lan:5001",
            SignalRHubUrl = "ws://admin.cafe.lan:5001/hubs/agent",
            EnvironmentName = "Production"
        };

        var result = _validator.Validate(null, options);
        Assert.True(result.Failed);
        Assert.Contains("must use WSS or HTTPS", result.FailureMessage);
    }

    [Fact]
    public void Validate_HttpOnLocalhostInDevelopment_ReturnsSuccess()
    {
        var options = new AgentConfiguration
        {
            BackendApiBaseUrl = "http://localhost:5000",
            SignalRHubUrl = "http://localhost:5000/hubs/agent",
            EnvironmentName = "Development"
        };

        var result = _validator.Validate(null, options);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_InvalidHeartbeatInterval_ReturnsFailure()
    {
        var options = new AgentConfiguration
        {
            BackendApiBaseUrl = "https://127.0.0.1:5001",
            SignalRHubUrl = "https://127.0.0.1:5001/hubs/agent",
            HeartbeatIntervalSeconds = 2
        };

        var result = _validator.Validate(null, options);
        Assert.True(result.Failed);
        Assert.Contains("HeartbeatIntervalSeconds must be at least 5", result.FailureMessage);
    }

    [Fact]
    public void Validate_PinnedThumbprintModeWithoutThumbprint_ReturnsFailure()
    {
        var options = new AgentConfiguration
        {
            BackendApiBaseUrl = "https://127.0.0.1:5001",
            SignalRHubUrl = "https://127.0.0.1:5001/hubs/agent",
            TlsValidationMode = "PinnedThumbprint",
            PinnedCertificateThumbprint = ""
        };

        var result = _validator.Validate(null, options);
        Assert.True(result.Failed);
        Assert.Contains("PinnedCertificateThumbprint is required", result.FailureMessage);
    }
}
