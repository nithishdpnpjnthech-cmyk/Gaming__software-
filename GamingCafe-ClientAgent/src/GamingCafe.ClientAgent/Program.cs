using GamingCafe.ClientAgent.Authentication;
using GamingCafe.ClientAgent.Communication;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Enrollment;
using GamingCafe.ClientAgent.Health;
using GamingCafe.ClientAgent.Heartbeat;
using GamingCafe.ClientAgent.Identity;
using GamingCafe.ClientAgent.Logging;
using GamingCafe.ClientAgent.Messaging;
using GamingCafe.ClientAgent.Security;
using GamingCafe.ClientAgent.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = CreateHostBuilder(args);
        var host = builder.Build();

        var logger = host.Services.GetRequiredService<ILogger<ClientAgentWorker>>();
        logger.LogInformation("Starting Gaming Cafe Client Agent (Phase 1 Windows Service)...");

        await host.RunAsync();
    }

    public static HostApplicationBuilder CreateHostBuilder(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Windows Service hosting configuration
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "GamingCafe Client Agent";
        });

        // Ensure configuration files are resolved regardless of launch CWD
        var candidateDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppContext.BaseDirectory,
            builder.Environment.ContentRootPath,
            Directory.GetCurrentDirectory(),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "GamingCafe.ClientAgent")
        };

        foreach (var dir in candidateDirs)
        {
            if (Directory.Exists(dir))
            {
                var mainJson = Path.Combine(dir, "appsettings.json");
                var envJson = Path.Combine(dir, $"appsettings.{builder.Environment.EnvironmentName}.json");

                if (File.Exists(mainJson))
                {
                    builder.Configuration.AddJsonFile(mainJson, optional: true, reloadOnChange: true);
                }
                if (File.Exists(envJson))
                {
                    builder.Configuration.AddJsonFile(envJson, optional: true, reloadOnChange: true);
                }
            }
        }
        builder.Configuration.AddEnvironmentVariables();

        // 1. Independent File Logger Options extracted cleanly from raw IConfiguration
        var loggingConfig = builder.Configuration.GetSection("Logging:File");
        var logDir = loggingConfig["LogDirectory"] ?? "logs";
        var resolvedLogDir = Path.IsPathRooted(logDir) ? logDir : Path.Combine(AppContext.BaseDirectory, logDir);
        var maxMb = int.TryParse(loggingConfig["MaxFileSizeMb"], out var parsedMb) ? parsedMb : 10;
        var retainDays = int.TryParse(loggingConfig["RetainedFileDays"], out var parsedDays) ? parsedDays : 14;

        var fileLoggerOptions = new FileLoggerOptions
        {
            LogDirectory = resolvedLogDir,
            MaxFileSizeMb = maxMb,
            RetainedFileDays = retainDays
        };

        builder.Services.AddSingleton(fileLoggerOptions);
        builder.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>();
        builder.Services.AddSingleton<IStructuredLoggingService, StructuredLoggingService>();

        // 2. Configuration Service
        builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();

        // 3. Security
        builder.Services.AddSingleton<ICredentialProtector, DpapiCredentialProtection>();
        builder.Services.AddSingleton<ISecurityValidationService, SecurityValidationService>();

        // 4. Identity
        builder.Services.AddSingleton<IDeviceIdentityStore, DeviceIdentityStore>();
        builder.Services.AddSingleton<IDeviceIdentityService, DeviceIdentityService>();

        // 5. Authentication
        builder.Services.AddSingleton<ISecureCredentialStore, SecureCredentialStore>();
        builder.Services.AddSingleton<IDeviceAuthenticationService, DeviceAuthenticationService>();

        // 6. Messaging
        builder.Services.AddSingleton<IMessageHandler, MessageHandler>();

        // 7. Enrollment
        builder.Services.AddSingleton<IDeviceEnrollmentService, DeviceEnrollmentService>();

        // 8. Communication
        builder.Services.AddSingleton(sp =>
        {
            var secService = sp.GetRequiredService<ISecurityValidationService>();
            var handler = new SocketsHttpHandler
            {
                SslOptions =
                {
                    RemoteCertificateValidationCallback = (sender, cert, chain, errors) =>
                    {
                        var cert2 = cert as System.Security.Cryptography.X509Certificates.X509Certificate2;
                        return secService.ValidateServerCertificate(null, cert2, chain, errors);
                    }
                }
            };
            return new HttpClient(handler);
        });
        builder.Services.AddSingleton<IBackendApiClient, BackendApiClient>();
        builder.Services.AddSingleton<IConnectionStateManager, ConnectionStateManager>();
        builder.Services.AddSingleton<IReconnectionService, ReconnectionService>();
        builder.Services.AddSingleton<ISignalRConnectionManager, SignalRConnectionManager>();

        // 9. Heartbeat & Health
        builder.Services.AddSingleton<IAgentHealthService, AgentHealthService>();
        builder.Services.AddSingleton<IHeartbeatService, HeartbeatService>();

        // 10. Worker Service
        builder.Services.AddHostedService<ClientAgentWorker>();

        return builder;
    }
}
