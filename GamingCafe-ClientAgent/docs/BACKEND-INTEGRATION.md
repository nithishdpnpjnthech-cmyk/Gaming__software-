# Gaming Cafe Backend Integration Guide

## 1. Overview

The Gaming Cafe Client Agent integrates with your ASP.NET Core backend server via **SignalR over WebSocket Secure (WSS)**. 

---

## 2. ASP.NET Core Hub Implementation (`AgentHub.cs`)

Add this Hub to your backend repository:

```csharp
using Microsoft.AspNetCore.SignalR;
using GamingCafe.ClientAgent.Messaging; // Or backend equivalent message models

namespace GamingCafe.Backend.Hubs;

public class AgentHub : Hub
{
    private readonly ILogger<AgentHub> _logger;

    public AgentHub(ILogger<AgentHub> logger)
    {
        _logger = logger;
    }

    // Called by Client Agent to register for the first time
    public async Task RegisterDevice(DeviceRegistrationMessage registration)
    {
        _logger.LogInformation("Agent registration received from {DeviceName} ({DeviceId})",
            registration.DeviceName, registration.DeviceId);

        // Store device in database under status 'PendingApproval'
        // If approved by administrator, send approval and authentication:
        await Clients.Caller.SendAsync("DeviceApproval", new DeviceApprovalMessage
        {
            DeviceId = registration.DeviceId,
            IsApproved = true,
            AssignedZone = "Standard-Zone"
        });

        await Clients.Caller.SendAsync("DeviceAuthentication", new DeviceAuthenticationMessage
        {
            DeviceId = registration.DeviceId,
            AccessToken = "jwt-generated-token-xyz",
            TokenExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(8)
        });
    }

    // Called periodically by Client Agent
    public async Task SendHeartbeat(HeartbeatMessage heartbeat)
    {
        _logger.LogDebug("Heartbeat received: Device {DeviceId}, Seq #{Seq}, CPU: {Cpu}%, Mem: {Mem}MB",
            heartbeat.DeviceId, heartbeat.SequenceNumber, heartbeat.CpuPercent, heartbeat.MemoryUsageMb);

        // Send HeartbeatAck to client
        await Clients.Caller.SendAsync("HeartbeatAck", new HeartbeatAcknowledgment
        {
            DeviceId = heartbeat.DeviceId,
            SequenceNumber = heartbeat.SequenceNumber,
            OriginalMessageId = heartbeat.MessageId,
            ServerTimestampUtc = DateTimeOffset.UtcNow
        });
    }
}
```

---

## 3. Server Startup Pipeline (`Program.cs`)

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();

var app = builder.Build();
app.UseHttpsRedirection();
app.MapHub<AgentHub>("/hubs/agent");
app.Run();
```
