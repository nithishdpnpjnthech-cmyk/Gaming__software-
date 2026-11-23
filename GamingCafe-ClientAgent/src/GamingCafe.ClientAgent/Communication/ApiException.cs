using System.Net;

namespace GamingCafe.ClientAgent.Communication;

/// <summary>
/// Exception representing an HTTP or network failure communicating with the Admin backend.
/// </summary>
public class ApiException : Exception
{
    public HttpStatusCode? StatusCode { get; }
    public bool IsNetworkOrServerUnavailable { get; }

    public ApiException(string message, HttpStatusCode? statusCode = null, bool isNetworkUnavailable = false, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        IsNetworkOrServerUnavailable = isNetworkUnavailable;
    }
}
