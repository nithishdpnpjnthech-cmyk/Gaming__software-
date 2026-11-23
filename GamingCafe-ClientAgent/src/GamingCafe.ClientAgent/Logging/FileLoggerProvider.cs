using System.Collections.Concurrent;
using System.Text;
using GamingCafe.ClientAgent.Security;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logDirectory;
    private readonly int _maxFileSizeMb;
    private readonly int _retainedDays;
    private readonly BlockingCollection<string> _messageQueue = new(10000);
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _writerTask;
    private bool _isDisposed;

    public FileLoggerProvider(FileLoggerOptions? options = null)
    {
        var opts = options ?? new FileLoggerOptions();
        _logDirectory = Path.GetFullPath(string.IsNullOrWhiteSpace(opts.LogDirectory) ? "logs" : opts.LogDirectory);
        _maxFileSizeMb = opts.MaxFileSizeMb > 0 ? opts.MaxFileSizeMb : 10;
        _retainedDays = opts.RetainedFileDays > 0 ? opts.RetainedFileDays : 14;

        try
        {
            Directory.CreateDirectory(_logDirectory);
        }
        catch
        {
            // Fail-safe if directory creation fails on locked storage
        }

        _writerTask = Task.Run(ProcessQueueAsync);
    }

    public FileLoggerProvider(string logDirectory, int maxFileSizeMb = 10, int retainedDays = 14)
        : this(new FileLoggerOptions
        {
            LogDirectory = logDirectory,
            MaxFileSizeMb = maxFileSizeMb,
            RetainedFileDays = retainedDays
        })
    {
    }

    public ILogger CreateLogger(string categoryName) =>
        new FileLogger(categoryName, _messageQueue);

    private async Task ProcessQueueAsync()
    {
        while (!_messageQueue.IsCompleted)
        {
            try
            {
                if (_messageQueue.TryTake(out var message, 1000, _cts.Token))
                {
                    await WriteToFileAsync(message).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Drain remaining queued messages
                while (_messageQueue.TryTake(out var remainingMessage))
                {
                    try
                    {
                        await WriteToFileAsync(remainingMessage).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Suppress write errors during shutdown
                    }
                }
                break;
            }
            catch
            {
                // Silently swallow file logger disk write errors to prevent service crash
            }
        }
    }

    private async Task WriteToFileAsync(string message)
    {
        var dateStr = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        var logFilePath = Path.Combine(_logDirectory, $"clientagent-{dateStr}.log");

        if (File.Exists(logFilePath))
        {
            var info = new FileInfo(logFilePath);
            if (info.Length > _maxFileSizeMb * 1024L * 1024L)
            {
                var timestamp = DateTimeOffset.UtcNow.ToString("HHmmss");
                var rotatedPath = Path.Combine(_logDirectory, $"clientagent-{dateStr}-{timestamp}.log");
                File.Move(logFilePath, rotatedPath, overwrite: true);
            }
        }

        await File.AppendAllTextAsync(logFilePath, message + Environment.NewLine, Encoding.UTF8).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _cts.Cancel();
        _messageQueue.CompleteAdding();
        try
        {
            _writerTask.Wait(TimeSpan.FromSeconds(3));
        }
        catch
        {
            // Suppress wait cancellation exceptions
        }
        _cts.Dispose();
        _messageQueue.Dispose();
    }
}

public sealed class FileLogger : ILogger
{
    private readonly string _category;
    private readonly BlockingCollection<string> _queue;

    public FileLogger(string category, BlockingCollection<string> queue)
    {
        _category = category;
        _queue = queue;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var rawMessage = formatter(state, exception);
        var sanitizedMessage = SecurityMasker.SanitizeLogMessage(rawMessage);

        var logEntry = $"{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss.fff zzz} [{logLevel,-5}] [{_category}] {sanitizedMessage}";
        if (exception != null)
        {
            logEntry += Environment.NewLine + exception.ToString();
        }

        _queue.TryAdd(logEntry);
    }
}
