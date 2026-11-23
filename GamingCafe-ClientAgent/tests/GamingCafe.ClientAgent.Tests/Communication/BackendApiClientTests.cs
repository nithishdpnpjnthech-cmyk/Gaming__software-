using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using GamingCafe.ClientAgent.Communication;
using GamingCafe.ClientAgent.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GamingCafe.ClientAgent.Tests.Communication;

public class BackendApiClientTests
{
    [Fact]
    public async Task BackendApiClient_MalformedJsonResponse_ReturnsHandledFailureWithoutCrash()
    {
        var mockHttp = new CustomHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ not-valid-json: [}}", Encoding.UTF8, "application/json")
        });

        var options = Microsoft.Extensions.Options.Options.Create(new AgentConfiguration
        {
            BackendApiBaseUrl = "https://127.0.0.1:5001"
        });

        var httpClient = new HttpClient(mockHttp) { BaseAddress = new Uri(options.Value.BackendApiBaseUrl) };
        var factory = new SimpleHttpClientFactory(httpClient);
        var client = new BackendApiClient(factory, options, NullLogger<BackendApiClient>.Instance);

        var response = await client.GetEnrollmentStatusAsync(Guid.NewGuid());

        Assert.False(response.IsSuccess);
        Assert.Contains("Malformed", response.ErrorMessage);
    }

    [Fact]
    public void ValidateServerCertificate_PinnedThumbprintMode_ValidatesThumbprintStrictly()
    {
        using var rsa = RSA.Create(2048);
        var certReq = new CertificateRequest("cn=TestCafeServer", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = certReq.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        var validThumbprint = cert.GetCertHashString(HashAlgorithmName.SHA256);

        var validOptions = new AgentConfiguration
        {
            TlsValidationMode = "PinnedThumbprint",
            PinnedCertificateThumbprint = validThumbprint
        };

        var invalidOptions = new AgentConfiguration
        {
            TlsValidationMode = "PinnedThumbprint",
            PinnedCertificateThumbprint = "00112233445566778899AABBCCDDEEFF00112233445566778899AABBCCDDEEFF"
        };

        var isValid = BackendApiClient.ValidateServerCertificate(
            new HttpRequestMessage(),
            cert,
            null,
            System.Net.Security.SslPolicyErrors.None,
            validOptions);

        var isInvalid = BackendApiClient.ValidateServerCertificate(
            new HttpRequestMessage(),
            cert,
            null,
            System.Net.Security.SslPolicyErrors.None,
            invalidOptions);

        Assert.True(isValid);
        Assert.False(isInvalid);
    }
}

public class CustomHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
    public CustomHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(_handler(request));
}

public class SimpleHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _client;
    public SimpleHttpClientFactory(HttpClient client) => _client = client;
    public HttpClient CreateClient(string name) => _client;
}
