using GamingCafe.ClientAgent.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GamingCafe.ClientAgent.Tests;

public class LoggerProviderLifecycleTests : IDisposable
{
    private readonly string _testLogDir;

    public LoggerProviderLifecycleTests()
    {
        _testLogDir = Path.Combine(Path.GetTempPath(), "AgentTest_Log_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testLogDir);
    }

    [Fact]
    public async Task FileLoggerProvider_ConcurrentLogging_AndDrainingOnDispose()
    {
        var options = new FileLoggerOptions
        {
            LogDirectory = _testLogDir,
            MaxFileSizeMb = 5,
            RetainedFileDays = 7
        };

        var provider = new FileLoggerProvider(options);
        var logger = provider.CreateLogger("TestCategory");

        // Concurrent multi-threaded write test
        var tasks = Enumerable.Range(1, 50).Select(i => Task.Run(() =>
        {
            logger.LogInformation("Thread message {Index} with sensitive token: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9", i);
        }));

        await Task.WhenAll(tasks);

        // Disposing flushes and drains the queue
        provider.Dispose();

        // Verify log file was written
        var logFiles = Directory.GetFiles(_testLogDir, "clientagent-*.log");
        Assert.NotEmpty(logFiles);

        var content = await File.ReadAllTextAsync(logFiles[0]);
        Assert.Contains("Thread message", content);

        // Verify secrets are redacted in log file
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9", content);
        Assert.Contains("token:eyJh...", content);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testLogDir))
            {
                Directory.Delete(_testLogDir, recursive: true);
            }
        }
        catch
        {
            // Suppress cleanup errors
        }
    }
}
