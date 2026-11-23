using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using GamingCafe.ClientAgent.Security;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Logging;

/// <summary>
/// Lightweight, zero-dependency rolling file logger with daily rotation, size limits,
/// and automated credential sanitization.
/// </summary>
public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logDirectory;
    private readonly long _maxFileSizeBytes;
    private readonly BlockingCollection<string> _logQueue = new(1000);
    private readonly Thread _loggingThread;
    private readonly CancellationTokenSource _cts = new();

    public FileLoggerProvider(string? logDirectory = null, long maxFileSizeBytes = 10 * 1024 * 1024)
    {
        _maxFileSizeBytes = maxFileSizeBytes;

        if (!string.IsNullOrWhiteSpace(logDirectory))
        {
            _logDirectory = Path.GetFullPath(logDirectory);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            _logDirectory = Path.Combine(programData, "GamingCafe", "ClientAgent", "logs");
        }
        else
        {
            _logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        }

        Directory.CreateDirectory(_logDirectory);

        _loggingThread = new Thread(ProcessLogQueue)
        {
            IsBackground = true,
            Name = "AgentFileLogger"
        };
        _loggingThread.Start();
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, this);

    public void EnqueueLog(string message)
    {
        if (!_cts.IsCancellationRequested)
        {
            _logQueue.TryAdd(message);
        }
    }

    private void ProcessLogQueue()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                if (_logQueue.TryTake(out var message, 500, _cts.Token))
                {
                    WriteToFile(message);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch { }
        }

        while (_logQueue.TryTake(out var message))
        {
            try { WriteToFile(message); } catch { }
        }
    }

    private void WriteToFile(string message)
    {
        try
        {
            var dateStr = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var filePath = Path.Combine(_logDirectory, $"agent-{dateStr}.log");

            if (File.Exists(filePath) && new FileInfo(filePath).Length > _maxFileSizeBytes)
            {
                var timestamp = DateTime.UtcNow.ToString("HHmmss");
                var rotated = Path.Combine(_logDirectory, $"agent-{dateStr}-{timestamp}.log");
                File.Move(filePath, rotated);
            }

            File.AppendAllText(filePath, message + Environment.NewLine);
        }
        catch { }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _loggingThread.Join(1000);
        _logQueue.Dispose();
        _cts.Dispose();
    }
}

public class FileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly FileLoggerProvider _provider;

    public FileLogger(string categoryName, FileLoggerProvider provider)
    {
        _categoryName = categoryName;
        _provider = provider;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        var sanitizedMessage = SecurityMasker.SanitizeLogMessage(message);

        var logLine = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}Z] [{logLevel.ToString().ToUpperInvariant()}] [{_categoryName}] {sanitizedMessage}";

        if (exception != null)
        {
            logLine += Environment.NewLine + $"Exception: {exception.GetType().FullName}: {exception.Message}";
            if (!string.IsNullOrWhiteSpace(exception.StackTrace))
            {
                logLine += Environment.NewLine + exception.StackTrace;
            }
        }

        _provider.EnqueueLog(logLine);
    }
}
