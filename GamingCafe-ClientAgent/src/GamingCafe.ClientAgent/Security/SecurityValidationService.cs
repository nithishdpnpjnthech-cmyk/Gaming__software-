using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using GamingCafe.ClientAgent.Configuration;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Security;

public sealed class SecurityValidationService : ISecurityValidationService
{
    private readonly IConfigurationService _configService;
    private readonly ILogger<SecurityValidationService> _logger;

    public SecurityValidationService(
        IConfigurationService configService,
        ILogger<SecurityValidationService> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool ValidateHubUrl(string hubUrl, out string? failureReason)
    {
        failureReason = null;
        if (string.IsNullOrWhiteSpace(hubUrl))
        {
            failureReason = "HubUrl is empty or null.";
            return false;
        }

        if (!Uri.TryCreate(hubUrl, UriKind.Absolute, out var uri))
        {
            failureReason = $"Invalid URI format: '{hubUrl}'.";
            return false;
        }

        var scheme = uri.Scheme.ToLowerInvariant();
        var requireWss = _configService.CurrentConfiguration.Backend.RequireWss ||
                         _configService.CurrentConfiguration.Security.RejectNonWss;

        if (requireWss && scheme != "wss" && scheme != "https")
        {
            failureReason = $"Insecure scheme '{scheme}'. WSS requires 'wss://' or 'https://'. Insecure ws:// and http:// are strictly rejected.";
            _logger.LogError("Security validation rejected HubUrl: {Reason}", failureReason);
            return false;
        }

        return true;
    }

    public bool ValidateServerCertificate(
        HttpRequestMessage? requestMessage,
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        var config = _configService.CurrentConfiguration.Backend;

        if (certificate == null)
        {
            _logger.LogError("TLS verification failed: Server provided no certificate.");
            return false;
        }

        // Pinned thumbprint check if configured
        if (!string.IsNullOrWhiteSpace(config.ExpectedCertificateThumbprint))
        {
            var serverThumbprint = certificate.Thumbprint;
            var expected = config.ExpectedCertificateThumbprint.Replace(":", "").Replace(" ", "").ToUpperInvariant();
            var matches = string.Equals(serverThumbprint, expected, StringComparison.OrdinalIgnoreCase);

            if (!matches)
            {
                _logger.LogError("TLS verification failed: Server certificate thumbprint mismatch. Expected '{Expected}', received '{Actual}'.",
                    SecurityMasker.Mask(expected), SecurityMasker.Mask(serverThumbprint));
                return false;
            }

            _logger.LogDebug("TLS certificate matched pinned thumbprint.");
            return true;
        }

        // Otherwise standard system validation check
        if (!config.ValidateCertificates)
        {
            if (_configService.CurrentConfiguration.Security.RejectNonWss)
            {
                _logger.LogError("Security policy violation: Certificate validation bypass is prohibited under active security hardening.");
                return false;
            }

            _logger.LogWarning("TLS certificate validation is DISABLED by development configuration.");
            return true;
        }

        if (sslPolicyErrors == SslPolicyErrors.None)
        {
            return true;
        }

        _logger.LogError("TLS certificate validation failed: {PolicyErrors}", sslPolicyErrors);
        return false;
    }
}
