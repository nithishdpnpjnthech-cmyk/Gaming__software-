using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Logging;

/// <summary>
/// Configures structured console, debug, and rolling file logging for the Client Agent.
/// </summary>
public static class LoggingConfiguration
{
    public static void ConfigureLogging(ILoggingBuilder logging, string? dataDirectory)
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.AddDebug();

        var logDirectory = string.IsNullOrWhiteSpace(dataDirectory) ? null : Path.Combine(dataDirectory, "logs");
        logging.AddProvider(new FileLoggerProvider(logDirectory));
    }
}
