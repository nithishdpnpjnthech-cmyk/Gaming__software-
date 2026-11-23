using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GamingCafe.ClientAgent.Tests;

public class SecurityValidationTests
{
    [Fact]
    public void ValidateHubUrl_RejectsInsecureSchemesWhenWssMandatory()
    {
        var config = new AgentConfiguration();
        config.Backend.RequireWss = true;
        var configService = new MockConfigurationService(config);
        var validator = new SecurityValidationService(configService, NullLogger<SecurityValidationService>.Instance);

        var resultWs = validator.ValidateHubUrl("ws://localhost:5001/hubs/agent", out var reasonWs);
        Assert.False(resultWs);
        Assert.Contains("WSS requires", reasonWs);

        var resultHttp = validator.ValidateHubUrl("http://localhost:5000/hubs/agent", out var reasonHttp);
        Assert.False(resultHttp);
        Assert.Contains("WSS requires", reasonHttp);

        var resultWss = validator.ValidateHubUrl("wss://localhost:5001/hubs/agent", out var reasonWss);
        Assert.True(resultWss);
        Assert.Null(reasonWss);
    }

    [Fact]
    public void SecurityMasker_MasksSensitiveValues()
    {
        var token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9";
        var masked = SecurityMasker.Mask(token);

        Assert.StartsWith("eyJh", masked);
        Assert.EndsWith("VCJ9", masked);
        Assert.Contains("...", masked);

        var logLine = "Connecting with token: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9 to backend";
        var sanitized = SecurityMasker.SanitizeLogMessage(logLine);

        Assert.DoesNotContain(token, sanitized);
        Assert.Contains("token:eyJh...", sanitized);
    }
}
