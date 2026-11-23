# Gaming Cafe Client Agent — Implementation Guide

## 1. Architecture Components Overview

The solution follows clean architectural separation across focused domain services:

```
src/GamingCafe.ClientAgent/
├── Authentication/          # Device credentials, secure storage, and token lifecycle
├── Communication/           # SignalR connection manager, state manager, reconnection
├── Configuration/           # Strongly-typed configuration, validator, and reloading
├── Enrollment/              # 8-stage enrollment workflow over SignalR
├── Health/                  # In-memory CPU, memory, thread, and latency metrics
├── Heartbeat/               # SignalR WSS heartbeat dispatcher and ACK tracking
├── Identity/                # Hardware fingerprinting, persistent GUID, atomic storage
├── Logging/                 # FileLoggerProvider (10MB rotation) & secret masker
├── Messaging/               # Protocol message schemas and sliding-window deduplicator
├── Security/                # DPAPI protector, TLS certificate validator, secret redaction
├── Services/                # ClientAgentWorker (BackgroundService)
└── Program.cs               # Service host builder & DI registration
```

---

## 2. Dependency Injection Registration

All components are registered in `Program.cs` as singletons managed by the .NET Generic Host:

```csharp
public static async Task Main(string[] args)
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "GamingCafe Client Agent";
    });

    builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();
    builder.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>();
    builder.Services.AddSingleton<IStructuredLoggingService, StructuredLoggingService>();
    builder.Services.AddSingleton<ICredentialProtector, DpapiCredentialProtection>();
    builder.Services.AddSingleton<ISecurityValidationService, SecurityValidationService>();
    builder.Services.AddSingleton<IDeviceIdentityStore, DeviceIdentityStore>();
    builder.Services.AddSingleton<IDeviceIdentityService, DeviceIdentityService>();
    builder.Services.AddSingleton<ISecureCredentialStore, SecureCredentialStore>();
    builder.Services.AddSingleton<IDeviceAuthenticationService, DeviceAuthenticationService>();
    builder.Services.AddSingleton<IMessageHandler, MessageHandler>();
    builder.Services.AddSingleton<IDeviceEnrollmentService, DeviceEnrollmentService>();
    builder.Services.AddSingleton<IConnectionStateManager, ConnectionStateManager>();
    builder.Services.AddSingleton<IReconnectionService, ReconnectionService>();
    builder.Services.AddSingleton<ISignalRConnectionManager, SignalRConnectionManager>();
    builder.Services.AddSingleton<IAgentHealthService, AgentHealthService>();
    builder.Services.AddSingleton<IHeartbeatService, HeartbeatService>();
    builder.Services.AddHostedService<ClientAgentWorker>();

    var host = builder.Build();
    await host.RunAsync();
}
```

---

## 3. Extending the Agent in Future Phases

To register new inbound SignalR messages from the Hub:
1. Define the message contract inheriting from `BaseMessage` in `Messaging/ProtocolMessages.cs`.
2. Register a handler with `IMessageHandler.RegisterHandler<T>("MethodName", async msg => { ... })`.
3. Add the Hub listener in `SignalRConnectionManager.RegisterHubEvents`.
