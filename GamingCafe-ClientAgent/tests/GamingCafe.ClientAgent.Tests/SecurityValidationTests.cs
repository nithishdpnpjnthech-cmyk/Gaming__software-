using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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

    [Fact]
    public void ValidateServerCertificate_WhenNullCertificate_ReturnsFalse()
    {
        var config = new AgentConfiguration();
        var configService = new MockConfigurationService(config);
        var validator = new SecurityValidationService(configService, NullLogger<SecurityValidationService>.Instance);

        var result = validator.ValidateServerCertificate(null, null, null, SslPolicyErrors.None);
        Assert.False(result);
    }

    [Fact]
    public void ValidateServerCertificate_WhenExpiredCertificate_ReturnsFalse()
    {
        var config = new AgentConfiguration();
        var configService = new MockConfigurationService(config);
        var validator = new SecurityValidationService(configService, NullLogger<SecurityValidationService>.Instance);

        using var expiredCert = CreateCertificate("expired.gamingcafe.local", 
            DateTimeOffset.UtcNow.AddDays(-30), 
            DateTimeOffset.UtcNow.AddDays(-1));

        var result = validator.ValidateServerCertificate(null, expiredCert, null, SslPolicyErrors.None);
        Assert.False(result);
    }

    [Fact]
    public void ValidateServerCertificate_WhenNotYetValidCertificate_ReturnsFalse()
    {
        var config = new AgentConfiguration();
        var configService = new MockConfigurationService(config);
        var validator = new SecurityValidationService(configService, NullLogger<SecurityValidationService>.Instance);

        using var futureCert = CreateCertificate("future.gamingcafe.local", 
            DateTimeOffset.UtcNow.AddDays(10), 
            DateTimeOffset.UtcNow.AddDays(40));

        var result = validator.ValidateServerCertificate(null, futureCert, null, SslPolicyErrors.None);
        Assert.False(result);
    }

    [Fact]
    public void ValidateServerCertificate_WhenHostnameMismatch_ReturnsFalse()
    {
        var config = new AgentConfiguration();
        var configService = new MockConfigurationService(config);
        var validator = new SecurityValidationService(configService, NullLogger<SecurityValidationService>.Instance);

        using var cert = CreateCertificate("backend.gamingcafe.local", 
            DateTimeOffset.UtcNow.AddDays(-1), 
            DateTimeOffset.UtcNow.AddDays(30));

        var result = validator.ValidateServerCertificate(null, cert, null, SslPolicyErrors.RemoteCertificateNameMismatch);
        Assert.False(result);
    }

    [Fact]
    public void ValidateServerCertificate_WhenChainErrors_ReturnsFalse()
    {
        var config = new AgentConfiguration();
        var configService = new MockConfigurationService(config);
        var validator = new SecurityValidationService(configService, NullLogger<SecurityValidationService>.Instance);

        using var cert = CreateCertificate("backend.gamingcafe.local", 
            DateTimeOffset.UtcNow.AddDays(-1), 
            DateTimeOffset.UtcNow.AddDays(30));

        var result = validator.ValidateServerCertificate(null, cert, null, SslPolicyErrors.RemoteCertificateChainErrors);
        Assert.False(result);
    }

    [Fact]
    public void ValidateServerCertificate_WhenThumbprintMismatch_ReturnsFalse()
    {
        var config = new AgentConfiguration();
        config.Backend.ExpectedCertificateThumbprint = "00112233445566778899AABBCCDDEEFF00112233";
        var configService = new MockConfigurationService(config);
        var validator = new SecurityValidationService(configService, NullLogger<SecurityValidationService>.Instance);

        using var cert = CreateCertificate("backend.gamingcafe.local", 
            DateTimeOffset.UtcNow.AddDays(-1), 
            DateTimeOffset.UtcNow.AddDays(30));

        var result = validator.ValidateServerCertificate(null, cert, null, SslPolicyErrors.None);
        Assert.False(result);
    }

    [Fact]
    public void ValidateServerCertificate_WhenThumbprintMatches_ReturnsTrue()
    {
        using var cert = CreateCertificate("backend.gamingcafe.local", 
            DateTimeOffset.UtcNow.AddDays(-1), 
            DateTimeOffset.UtcNow.AddDays(30));

        var config = new AgentConfiguration();
        config.Backend.ExpectedCertificateThumbprint = cert.Thumbprint;
        var configService = new MockConfigurationService(config);
        var validator = new SecurityValidationService(configService, NullLogger<SecurityValidationService>.Instance);

        var result = validator.ValidateServerCertificate(null, cert, null, SslPolicyErrors.RemoteCertificateChainErrors);
        Assert.True(result);
    }

    [Fact]
    public void ValidateServerCertificate_WhenValidCertificateAndNoPolicyErrors_ReturnsTrue()
    {
        var config = new AgentConfiguration();
        var configService = new MockConfigurationService(config);
        var validator = new SecurityValidationService(configService, NullLogger<SecurityValidationService>.Instance);

        using var cert = CreateCertificate("backend.gamingcafe.local", 
            DateTimeOffset.UtcNow.AddDays(-1), 
            DateTimeOffset.UtcNow.AddDays(30));

        var result = validator.ValidateServerCertificate(null, cert, null, SslPolicyErrors.None);
        Assert.True(result);
    }

    [Fact]
    public void ValidateServerCertificate_InDevelopment_WhenValidateCertificatesFalse_AllowsSelfSignedOrUntrustedCert()
    {
        var config = new AgentConfiguration();
        config.Agent.Environment = "Development";
        config.Backend.ValidateCertificates = false;
        config.Backend.RequireWss = true;
        config.Security.RejectNonWss = true;

        var configService = new MockConfigurationService(config);
        var validator = new SecurityValidationService(configService, NullLogger<SecurityValidationService>.Instance);

        using var cert = CreateCertificate("localhost", 
            DateTimeOffset.UtcNow.AddDays(-1), 
            DateTimeOffset.UtcNow.AddDays(30));

        // In Development with ValidateCertificates=false, RemoteCertificateChainErrors (e.g. self-signed) is bypassed
        var result = validator.ValidateServerCertificate(null, cert, null, SslPolicyErrors.RemoteCertificateChainErrors);
        Assert.True(result);
    }

    [Fact]
    public void ValidateServerCertificate_InProduction_WhenValidateCertificatesFalse_RejectsBypass()
    {
        var config = new AgentConfiguration();
        config.Agent.Environment = "Production";
        config.Backend.ValidateCertificates = false;

        var configService = new MockConfigurationService(config);
        var validator = new SecurityValidationService(configService, NullLogger<SecurityValidationService>.Instance);

        using var cert = CreateCertificate("backend.gamingcafe.local", 
            DateTimeOffset.UtcNow.AddDays(-1), 
            DateTimeOffset.UtcNow.AddDays(30));

        // In Production, ValidateCertificates=false must be rejected by security policy
        var result = validator.ValidateServerCertificate(null, cert, null, SslPolicyErrors.None);
        Assert.False(result);
    }

    private static X509Certificate2 CreateCertificate(string subject, DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest($"CN={subject}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return req.CreateSelfSigned(notBefore, notAfter);
    }
}
