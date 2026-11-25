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

        // 1. Certificate Expiry Validation (Universal check across production and development)
        var now = DateTime.UtcNow;
        if (now < certificate.NotBefore.ToUniversalTime() || now > certificate.NotAfter.ToUniversalTime())
        {
            _logger.LogError("TLS verification failed: Certificate is expired or not yet valid. NotBefore: {NotBefore:u}, NotAfter: {NotAfter:u}, CurrentUtc: {Now:u}.",
                certificate.NotBefore, certificate.NotAfter, now);
            return false;
        }

        // 2. Pinned thumbprint check if explicitly configured
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

        // 3. Permissive validation check - strictly isolated and permitted ONLY in Development environment
        if (!config.ValidateCertificates)
        {
            var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") 
                      ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                      ?? _configService.CurrentConfiguration.Agent.Environment;
            var isDevelopment = string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase);

            if (!isDevelopment)
            {
                _logger.LogError("Security policy violation: Certificate validation bypass is strictly prohibited in Production environment.");
                return false;
            }

            _logger.LogWarning("TLS certificate validation is BYPASSED by explicit Development configuration. TLS transport encryption remains active.");
            return true;
        }

        // 4. Hostname Validation
        if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
        {
            _logger.LogError("TLS verification failed: Hostname mismatch between target server and certificate Subject/SAN.");
            return false;
        }

        // 5. Certificate Chain and Authority Validation
        if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateChainErrors) != 0)
        {
            if (chain != null)
            {
                foreach (var status in chain.ChainStatus)
                {
                    _logger.LogError("TLS chain error: {StatusFlag} - {StatusInfo}", status.Status, status.StatusInformation?.Trim());
                }
            }
            else
            {
                _logger.LogError("TLS verification failed: Remote certificate chain errors detected but no chain was provided.");
            }
            return false;
        }

        if (sslPolicyErrors == SslPolicyErrors.None)
        {
            return true;
        }

        _logger.LogError("TLS certificate validation failed: {PolicyErrors}", sslPolicyErrors);
        return false;
    }
}
