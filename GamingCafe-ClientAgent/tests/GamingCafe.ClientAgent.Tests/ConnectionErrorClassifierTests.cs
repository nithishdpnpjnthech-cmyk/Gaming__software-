using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using GamingCafe.ClientAgent.Communication;
using Xunit;

namespace GamingCafe.ClientAgent.Tests;

public class ConnectionErrorClassifierTests
{
    [Fact]
    public void Classify_TlsException_ReturnsTlsFailure()
    {
        var authEx = new AuthenticationException("The remote certificate is invalid according to the validation procedure.");
        var (category, reason) = ConnectionErrorClassifier.Classify(authEx);

        Assert.Equal(ConnectionFailureCategory.TlsFailure, category);
        Assert.Contains("TLS/Certificate validation failed", reason);
    }

    [Fact]
    public void Classify_NestedTlsException_ReturnsTlsFailure()
    {
        var inner = new AuthenticationException("RemoteCertificateNameMismatch");
        var outer = new HttpRequestException("The SSL connection could not be established, see inner exception.", inner);

        var (category, reason) = ConnectionErrorClassifier.Classify(outer);

        Assert.Equal(ConnectionFailureCategory.TlsFailure, category);
        Assert.Contains("validation failed", reason);
    }

    [Fact]
    public void Classify_Unauthorized401_ReturnsAuthenticationFailure()
    {
        var httpEx = new HttpRequestException("Response status code does not indicate success: 401 (Unauthorized).", null, HttpStatusCode.Unauthorized);
        var (category, reason) = ConnectionErrorClassifier.Classify(httpEx);

        Assert.Equal(ConnectionFailureCategory.AuthenticationFailure, category);
        Assert.Contains("Authentication rejected by backend", reason);
    }

    [Fact]
    public void Classify_Forbidden403_ReturnsAuthenticationFailure()
    {
        var httpEx = new HttpRequestException("Response status code does not indicate success: 403 (Forbidden).", null, HttpStatusCode.Forbidden);
        var (category, reason) = ConnectionErrorClassifier.Classify(httpEx);

        Assert.Equal(ConnectionFailureCategory.AuthenticationFailure, category);
        Assert.Contains("403 Forbidden", reason);
    }

    [Fact]
    public void Classify_SocketExceptionConnectionRefused_ReturnsBackendUnavailable()
    {
        var sockEx = new SocketException((int)SocketError.ConnectionRefused);
        var httpEx = new HttpRequestException("No connection could be made because the target machine actively refused it.", sockEx);

        var (category, reason) = ConnectionErrorClassifier.Classify(httpEx);

        Assert.Equal(ConnectionFailureCategory.BackendUnavailable, category);
        Assert.Contains("Backend server unavailable", reason);
    }

    [Fact]
    public void Classify_OperationCanceled_ReturnsCancelled()
    {
        var cancelEx = new OperationCanceledException();
        var (category, reason) = ConnectionErrorClassifier.Classify(cancelEx);

        Assert.Equal(ConnectionFailureCategory.Cancelled, category);
        Assert.Contains("cancelled", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Classify_GeneralException_ReturnsOther()
    {
        var ex = new InvalidOperationException("Unexpected internal state failure.");
        var (category, reason) = ConnectionErrorClassifier.Classify(ex);

        Assert.Equal(ConnectionFailureCategory.Other, category);
        Assert.Equal("Unexpected internal state failure.", reason);
    }
}
