using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace GamingCafe.ClientAgent.Security;

public interface ISecurityValidationService
{
    bool ValidateHubUrl(string hubUrl, out string? failureReason);
    bool ValidateServerCertificate(
        HttpRequestMessage? requestMessage,
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors);
}
