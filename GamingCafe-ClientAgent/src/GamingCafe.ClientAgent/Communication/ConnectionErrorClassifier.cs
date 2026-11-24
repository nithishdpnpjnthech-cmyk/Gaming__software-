using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Authentication;

namespace GamingCafe.ClientAgent.Communication;

public enum ConnectionFailureCategory
{
    BackendUnavailable,
    AuthenticationFailure,
    TlsFailure,
    Cancelled,
    Other
}

public static class ConnectionErrorClassifier
{
    public static (ConnectionFailureCategory Category, string Reason) Classify(Exception? ex)
    {
        if (ex == null)
        {
            return (ConnectionFailureCategory.Other, "Unknown connection error");
        }

        if (ex is OperationCanceledException)
        {
            return (ConnectionFailureCategory.Cancelled, "Operation cancelled during agent shutdown");
        }

        // Traverse exception hierarchy to locate the specific root cause
        var current = ex;
        while (current != null)
        {
            // 1. TLS / SSL Certificate validation failure
            if (current is AuthenticationException ||
                current.Message.Contains("certificate", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("SSL", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("TLS", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("SecureChannelFailure", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("UntrustedRoot", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("RemoteCertificate", StringComparison.OrdinalIgnoreCase))
            {
                return (ConnectionFailureCategory.TlsFailure, $"TLS/Certificate validation failed: {current.Message}");
            }

            // 2. Authentication failure (HTTP 401 Unauthorized / HTTP 403 Forbidden)
            if (current is HttpRequestException httpEx)
            {
                if (httpEx.StatusCode == HttpStatusCode.Unauthorized || httpEx.StatusCode == HttpStatusCode.Forbidden)
                {
                    return (ConnectionFailureCategory.AuthenticationFailure,
                        $"Authentication rejected by backend (HTTP {(int)httpEx.StatusCode} {httpEx.StatusCode})");
                }
            }

            if (current.Message.Contains("401", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("403", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("Forbidden", StringComparison.OrdinalIgnoreCase))
            {
                return (ConnectionFailureCategory.AuthenticationFailure, $"Authentication rejected: {current.Message}");
            }

            // 3. Backend Unavailable / Socket / Connection refused
            if (current is SocketException sockEx)
            {
                return (ConnectionFailureCategory.BackendUnavailable,
                    $"Backend server unavailable ({sockEx.SocketErrorCode}: {sockEx.Message})");
            }

            current = current.InnerException;
        }

        if (ex is HttpRequestException || ex is WebSocketException)
        {
            return (ConnectionFailureCategory.BackendUnavailable, $"Backend server unavailable: {ex.Message}");
        }

        return (ConnectionFailureCategory.Other, ex.Message);
    }
}
