using System.Net;
using System.Text;

namespace GamingCafe.ClientAgent.Tests.TestDoubles;

/// <summary>
/// Controllable mock HttpMessageHandler allowing verification of HTTP calls and simulated responses.
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, HttpResponseMessage>? ResponseFactory { get; set; }
    public List<HttpRequestMessage> CapturedRequests { get; } = new();
    public List<string> CapturedRequestBodies { get; } = new();

    public void SetupJsonResponse<T>(T responseData, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(responseData);
        ResponseFactory = req => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    public void SetupStatusCode(HttpStatusCode statusCode, string content = "")
    {
        ResponseFactory = req => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "text/plain")
        };
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CapturedRequests.Add(request);

        if (request.Content != null)
        {
            var body = await request.Content.ReadAsStringAsync(cancellationToken);
            CapturedRequestBodies.Add(body);
        }
        else
        {
            CapturedRequestBodies.Add(string.Empty);
        }

        if (ResponseFactory != null)
        {
            return ResponseFactory(request);
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
    }
}
