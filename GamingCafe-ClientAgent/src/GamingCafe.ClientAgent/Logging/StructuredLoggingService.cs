using GamingCafe.ClientAgent.Security;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Logging;

public interface IStructuredLoggingService
{
    void LogInfo(string message, params object[] args);
    void LogWarning(string message, params object[] args);
    void LogError(Exception? ex, string message, params object[] args);
    void LogSecurityEvent(string eventType, string details);
}

public sealed class StructuredLoggingService : IStructuredLoggingService
{
    private readonly ILogger<StructuredLoggingService> _logger;

    public StructuredLoggingService(ILogger<StructuredLoggingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void LogInfo(string message, params object[] args)
    {
        _logger.LogInformation(SecurityMasker.SanitizeLogMessage(message), args);
    }

    public void LogWarning(string message, params object[] args)
    {
        _logger.LogWarning(SecurityMasker.SanitizeLogMessage(message), args);
    }

    public void LogError(Exception? ex, string message, params object[] args)
    {
        _logger.LogError(ex, "{Message}", SecurityMasker.SanitizeLogMessage(message));
    }

    public void LogSecurityEvent(string eventType, string details)
    {
        _logger.LogWarning("SECURITY_AUDIT: [{EventType}] {Details}", eventType, SecurityMasker.SanitizeLogMessage(details));
    }
}
