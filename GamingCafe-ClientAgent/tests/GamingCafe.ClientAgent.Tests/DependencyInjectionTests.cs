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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GamingCafe.ClientAgent.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void ProgramHostBuilder_ResolvesAllServices_WithoutCircularDependency()
    {
        var builder = GamingCafe.ClientAgent.Program.CreateHostBuilder(Array.Empty<string>());
        using var host = builder.Build();

        // 1. Core Logging & Configuration (previously circular)
        var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
        Assert.NotNull(loggerFactory);

        var configService = host.Services.GetRequiredService<IConfigurationService>();
        Assert.NotNull(configService);
        Assert.NotNull(configService.CurrentConfiguration);

        var loggerProvider = host.Services.GetRequiredService<ILoggerProvider>();
        Assert.NotNull(loggerProvider);
        Assert.IsType<FileLoggerProvider>(loggerProvider);

        var structuredLog = host.Services.GetRequiredService<IStructuredLoggingService>();
        Assert.NotNull(structuredLog);

        // 2. Security & Identity
        var protector = host.Services.GetRequiredService<ICredentialProtector>();
        Assert.NotNull(protector);

        var secVal = host.Services.GetRequiredService<ISecurityValidationService>();
        Assert.NotNull(secVal);

        var idStore = host.Services.GetRequiredService<IDeviceIdentityStore>();
        Assert.NotNull(idStore);

        var idService = host.Services.GetRequiredService<IDeviceIdentityService>();
        Assert.NotNull(idService);

        // 3. Authentication
        var credStore = host.Services.GetRequiredService<ISecureCredentialStore>();
        Assert.NotNull(credStore);

        var authService = host.Services.GetRequiredService<IDeviceAuthenticationService>();
        Assert.NotNull(authService);

        // 4. Communication & Messaging
        var msgHandler = host.Services.GetRequiredService<IMessageHandler>();
        Assert.NotNull(msgHandler);

        var enrollService = host.Services.GetRequiredService<IDeviceEnrollmentService>();
        Assert.NotNull(enrollService);

        var connState = host.Services.GetRequiredService<IConnectionStateManager>();
        Assert.NotNull(connState);

        var reconnService = host.Services.GetRequiredService<IReconnectionService>();
        Assert.NotNull(reconnService);

        var connManager = host.Services.GetRequiredService<ISignalRConnectionManager>();
        Assert.NotNull(connManager);

        // 5. Health & Heartbeat
        var healthService = host.Services.GetRequiredService<IAgentHealthService>();
        Assert.NotNull(healthService);

        var heartbeatService = host.Services.GetRequiredService<IHeartbeatService>();
        Assert.NotNull(heartbeatService);

        // 6. Hosted Background Worker
        var hostedServices = host.Services.GetServices<IHostedService>().ToList();
        Assert.NotEmpty(hostedServices);
    }

    [Fact]
    public void ServiceProviderValidation_WithValidateOnBuildAndScopes_Succeeds()
    {
        var builder = GamingCafe.ClientAgent.Program.CreateHostBuilder(Array.Empty<string>());

        // Force rigorous DI validation on build and scopes
        var sp = builder.Services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.NotNull(sp);
        using var scope = sp.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISignalRConnectionManager>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ILoggerFactory>());
    }
}
