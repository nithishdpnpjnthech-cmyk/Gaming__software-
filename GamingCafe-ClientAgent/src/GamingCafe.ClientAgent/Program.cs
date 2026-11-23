using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using GamingCafe.ClientAgent.Authentication;
using GamingCafe.ClientAgent.Communication;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Enrollment;
using GamingCafe.ClientAgent.Health;
using GamingCafe.ClientAgent.Heartbeat;
using GamingCafe.ClientAgent.Identity;
using GamingCafe.ClientAgent.Logging;
using GamingCafe.ClientAgent.Realtime;
using GamingCafe.ClientAgent.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GamingCafe.ClientAgent;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var builder = Host.CreateApplicationBuilder(args);

            // Configure Windows Service hosting with the required service name
            builder.Services.AddWindowsService(options =>
            {
                options.ServiceName = "GamingCafe Client Agent";
            });

            // Bind strongly-typed configuration options and startup validator
            builder.Services.Configure<AgentConfiguration>(
                builder.Configuration.GetSection(AgentConfiguration.SectionName));
            builder.Services.AddSingleton<IValidateOptions<AgentConfiguration>, ConfigurationValidator>();

            // Setup structured console and rolling file logging
            var dataDir = builder.Configuration.GetValue<string>($"{AgentConfiguration.SectionName}:LocalDataDirectory");
            LoggingConfiguration.ConfigureLogging(builder.Logging, dataDir);

            // Security & Credential Protection (Windows DPAPI)
            if (OperatingSystem.IsWindows())
            {
                builder.Services.AddSingleton<ICredentialProtection, DpapiCredentialProtection>();
            }
            else
            {
                builder.Services.AddSingleton<ICredentialProtection, PlainTextTestCredentialProtection>();
            }

            builder.Services.AddSingleton<IDeviceCredentialStore, DeviceCredentialStore>();
            builder.Services.AddSingleton<IDeviceAuthenticationService, DeviceAuthenticationService>();

            // Device Identity
            builder.Services.AddSingleton<IDeviceIdentityStore, DeviceIdentityStore>();
            builder.Services.AddSingleton<IDeviceIdentityService, DeviceIdentityService>();

            // Health Tracking
            builder.Services.AddSingleton<IAgentHealthService, AgentHealthService>();

            // Resilient REST API Client
            builder.Services.AddHttpClient(BackendApiClient.HttpClientName, (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<AgentConfiguration>>().Value;
                client.BaseAddress = new Uri(options.BackendApiBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds);
                client.DefaultRequestHeaders.Add("User-Agent", $"GamingCafe-Agent/{options.AgentVersion} ({Environment.OSVersion})");
            })
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var options = sp.GetRequiredService<IOptions<AgentConfiguration>>().Value;
                var handler = new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                    SslOptions = new SslClientAuthenticationOptions
                    {
                        RemoteCertificateValidationCallback = (sender, cert, chain, errors) =>
                        {
                            var x509Cert = cert as X509Certificate2 ?? (cert != null ? new X509Certificate2(cert) : null);
                            return BackendApiClient.ValidateServerCertificate(
                                (HttpRequestMessage)(sender is HttpRequestMessage req ? req : new HttpRequestMessage()),
                                x509Cert,
                                chain,
                                errors,
                                options);
                        }
                    }
                };
                return handler;
            });

            builder.Services.AddSingleton<IBackendApiClient, BackendApiClient>();

            // Realtime SignalR WebSocket Client
            builder.Services.AddSingleton<ISignalRConnectionManager, SignalRConnectionManager>();
            builder.Services.AddSingleton<ISignalRHubClient, SignalRHubClient>();

            // Enrollment & Heartbeat
            builder.Services.AddSingleton<IEnrollmentService, EnrollmentService>();
            builder.Services.AddSingleton<IHeartbeatService, HeartbeatService>();

            // Register Background Worker
            builder.Services.AddHostedService<Worker>();

            var host = builder.Build();

            // Perform eager startup validation
            var validator = host.Services.GetRequiredService<IValidateOptions<AgentConfiguration>>();
            var currentConfig = host.Services.GetRequiredService<IOptions<AgentConfiguration>>().Value;
            var validationResult = validator.Validate(null, currentConfig);
            if (validationResult.Failed)
            {
                var logger = host.Services.GetRequiredService<ILogger<Program>>();
                logger.LogCritical("Agent configuration validation failed: {Failures}", validationResult.FailureMessage);
                return 1;
            }

            await host.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal exception in Gaming Cafe Client Agent: {ex.Message}");
            return 1;
        }
    }
}
