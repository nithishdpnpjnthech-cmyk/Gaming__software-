using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using GamingCafe.Api.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GamingCafe.Api.Services;

public sealed class DeviceMonitoringConsole : IDeviceMonitoringConsole
{
    private static readonly object ConsoleLock = new();
    private readonly ILogger<DeviceMonitoringConsole> _logger;
    private readonly IOptionsMonitor<HeartbeatOptions> _heartbeatOptions;
    private readonly ConcurrentDictionary<Guid, long> _heartbeatCounters = new();

    public DeviceMonitoringConsole(
        ILogger<DeviceMonitoringConsole> logger,
        IOptionsMonitor<HeartbeatOptions> heartbeatOptions)
    {
        _logger = logger;
        _heartbeatOptions = heartbeatOptions;
    }

    private static string Timestamp() => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");

    private static void WriteColoredPrefix(string prefix, ConsoleColor color, string message)
    {
        lock (ConsoleLock)
        {
            var origColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{Timestamp()}] ");

            Console.ForegroundColor = color;
            Console.Write($"{prefix,-12} ");

            Console.ForegroundColor = origColor;
            Console.WriteLine(message);
        }
    }

    public void RenderStartupDashboard(
        string appName,
        string appVersion,
        string environment,
        IEnumerable<string> listeningUrls,
        string? apiBaseUrl,
        string? openApiUrl,
        string postgresHost,
        int postgresPort,
        string postgresDatabase,
        bool postgresConnected,
        string hubUrl,
        int heartbeatInterval,
        int heartbeatTimeout,
        string heartbeatLoggingMode,
        string monitoringServiceStatus)
    {
        var urlsList = listeningUrls.ToList();
        var urlsDisplay = urlsList.Count > 0 ? string.Join(", ", urlsList) : "Configured via Kestrel";
        var primaryUrl = urlsList.FirstOrDefault() ?? apiBaseUrl ?? "http://0.0.0.0:5022";
        var swaggerDisplay = !string.IsNullOrWhiteSpace(openApiUrl) ? openApiUrl : $"{primaryUrl.TrimEnd('/')}/openapi/v1.json";

        lock (ConsoleLock)
        {
            var orig = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==================================================================================================");
            Console.WriteLine($"   {appName.ToUpperInvariant()} - LIVE SYSTEM MONITORING DASHBOARD");
            Console.WriteLine("==================================================================================================");
            Console.ForegroundColor = orig;

            Console.WriteLine($"  Application:        {appName} v{appVersion}");
            Console.WriteLine($"  Environment:        {environment}");
            Console.WriteLine($"  Listening URLs:     {urlsDisplay}");
            Console.WriteLine($"  API Base URL:       {primaryUrl}");
            Console.WriteLine($"  OpenAPI / Swagger:  {swaggerDisplay}");
            Console.WriteLine($"  PostgreSQL:         Host={postgresHost}, Port={postgresPort}, Database={postgresDatabase} [{(postgresConnected ? "CONNECTED" : "FAILED")}]");
            Console.WriteLine($"  SignalR Agent Hub:  {hubUrl} (Auth: agent.heartbeat)");
            Console.WriteLine($"  Heartbeat Config:   Interval={heartbeatInterval}s, Timeout={heartbeatTimeout}s, Mode={heartbeatLoggingMode}");
            Console.WriteLine($"  Offline Monitor:    {monitoringServiceStatus}");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==================================================================================================");
            Console.ForegroundColor = orig;
        }

        LogWaitingForRegistrations();
    }

    public void LogWaitingForRegistrations(string endpoint = "POST /api/devices/register")
    {
        WriteColoredPrefix("[WAITING]", ConsoleColor.Yellow,
            $"Ready and waiting for client device registrations on '{endpoint}'");
    }

    public void LogRegistrationRequest(
        string displayName,
        string? deviceCode,
        Guid? localDeviceId,
        string? clientIp,
        string? agentVersion,
        string? hardwareFingerprint)
    {
        WriteColoredPrefix("[REQUEST]", ConsoleColor.Cyan,
            $"Device Registration Request Received from {clientIp ?? "Unknown IP"}");
        lock (ConsoleLock)
        {
            var orig = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"               |- Display Name:       '{displayName}'");
            Console.WriteLine($"               |- Requested Code:     '{deviceCode ?? "(Auto-generate)"}'");
            Console.WriteLine($"               |- Local Device ID:    {(localDeviceId.HasValue ? localDeviceId.Value.ToString() : "(None)")}");
            Console.WriteLine($"               |- Client IP Address:  {clientIp ?? "Unknown"}");
            Console.WriteLine($"               |- Agent Version:      {agentVersion ?? "Unknown"}");
            Console.WriteLine($"               |- Hardware Hash:      {hardwareFingerprint ?? "(None)"}");
            Console.ForegroundColor = orig;
        }
    }

    public void LogRegistrationValidationResult(bool success, string? details = null)
    {
        if (success)
        {
            WriteColoredPrefix("[PROCESSING]", ConsoleColor.Blue,
                $"Payload validation PASSED. Proceeding to database persistence...");
        }
        else
        {
            WriteColoredPrefix("[ERROR]", ConsoleColor.Red,
                $"Payload validation FAILED: {details}");
        }
    }

    public void LogRegistrationSuccess(
        Guid deviceId,
        string deviceCode,
        string displayName,
        string status,
        string approvalEndpoint,
        bool isExistingRecord = false)
    {
        WriteColoredPrefix("[SUCCESS]", ConsoleColor.Green,
            $"Database persistence completed: {(isExistingRecord ? "Found existing record" : "New record inserted")}");
        WriteColoredPrefix("[PENDING]", ConsoleColor.Yellow,
            $"Backend Device UUID: {deviceId} | Device Code: {deviceCode} | Status: {status}");

        lock (ConsoleLock)
        {
            var orig = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  +--------------------------------------------------------------------------------------------+");
            Console.WriteLine("  |                               DEVICE REGISTRATION SUCCESSFUL                               |");
            Console.WriteLine($"  | REGISTRATION STATUS: {status,-70}|");
            Console.WriteLine("  | APPROVAL STATUS:     WAITING FOR ADMIN                                                     |");
            Console.WriteLine($"  | APPROVAL ENDPOINT:   {approvalEndpoint,-70}|");
            Console.WriteLine("  +--------------------------------------------------------------------------------------------+");
            Console.ForegroundColor = orig;
        }
    }

    public void LogRegistrationFailure(string reason, int statusCode)
    {
        WriteColoredPrefix("[ERROR]", ConsoleColor.Red,
            $"Registration rejected ({statusCode}): {reason}");
    }

    public void LogApprovalRequest(Guid deviceId, Guid? adminId, string approvalRoute = "POST /api/devices/{id}/approve")
    {
        WriteColoredPrefix("[REQUEST]", ConsoleColor.Cyan,
            $"Approval Request Received via {approvalRoute} for DeviceId: {deviceId} by Admin: {adminId?.ToString() ?? "SuperAdmin"}");
    }

    public void LogApprovalSuccess(
        Guid deviceId,
        string deviceCode,
        Guid? adminId,
        DateTime approvedAt,
        string credentialStatus,
        string approvalRoute = "POST /api/devices/{id}/approve",
        string authEndpoint = "POST /api/auth/device/dev-login")
    {
        WriteColoredPrefix("[SUCCESS]", ConsoleColor.Green,
            $"Database update successful: registration_status = 'APPROVED', approved_by = {adminId?.ToString() ?? "Admin"}");
        WriteColoredPrefix("[APPROVED]", ConsoleColor.Green,
            $"Device {deviceCode} ({deviceId}) APPROVED successfully at {approvedAt:yyyy-MM-dd HH:mm:ss} UTC");
        WriteColoredPrefix("[SUCCESS]", ConsoleColor.Green,
            $"Credential provisioning complete: CertificateThumbprint hash enrolled, CredentialStatus = '{credentialStatus}'");

        lock (ConsoleLock)
        {
            var orig = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  +--------------------------------------------------------------------------------------------+");
            Console.WriteLine("  |                                DEVICE APPROVED SUCCESSFULLY                                |");
            Console.WriteLine("  | WAITING FOR CLIENT AUTHENTICATION                                                          |");
            Console.WriteLine($"  | APPROVAL ROUTE: {approvalRoute,-75}|");
            Console.WriteLine($"  | AUTH ENDPOINT:  {authEndpoint,-75}|");
            Console.WriteLine("  +--------------------------------------------------------------------------------------------+");
            Console.ForegroundColor = orig;
        }
    }

    public void LogRejection(Guid deviceId, string deviceCode, Guid? adminId, string reason)
    {
        WriteColoredPrefix("[REJECTED]", ConsoleColor.Red,
            $"Device {deviceCode} ({deviceId}) was REJECTED by Admin {adminId?.ToString() ?? "Admin"}. Reason: {reason}");
    }

    public void LogAuthenticationAttempt(Guid deviceId, string? clientIp)
    {
        WriteColoredPrefix("[PROCESSING]", ConsoleColor.Blue,
            $"Device Authentication Attempt received for DeviceId: {deviceId} from {clientIp ?? "Unknown IP"}");
    }

    public void LogAuthenticationResult(Guid deviceId, bool success, string? reason = null, DateTime? expiresAt = null)
    {
        if (success)
        {
            WriteColoredPrefix("[SUCCESS]", ConsoleColor.Green,
                $"Device Authentication SUCCESS: JWT device token issued for DeviceId: {deviceId} (Valid until: {expiresAt:yyyy-MM-dd HH:mm:ss} UTC)");
        }
        else
        {
            WriteColoredPrefix("[ERROR]", ConsoleColor.Red,
                $"Device Authentication FAILED for DeviceId: {deviceId}. Reason: {reason ?? "Invalid credentials"}");
        }
    }

    public void LogSignalRConnected(
        Guid deviceId,
        string? deviceCode,
        string connectionId,
        string? clientIp,
        bool isReconnect)
    {
        var label = isReconnect ? "Reconnected" : "Connected";
        WriteColoredPrefix("[ONLINE]", ConsoleColor.Green,
            $"Authenticated SignalR {label}! Device: '{deviceCode ?? "PC"}' ({deviceId}) | ConnId: '{connectionId}' | IP: {clientIp ?? "LAN"}");
    }

    public void LogSignalRDisconnected(
        Guid deviceId,
        string? deviceCode,
        string connectionId,
        string reason)
    {
        WriteColoredPrefix("[OFFLINE]", ConsoleColor.DarkYellow,
            $"SignalR Disconnected: Device '{deviceCode ?? "PC"}' ({deviceId}) | ConnId: '{connectionId}' | Reason: {reason}");
    }

    public void LogHeartbeat(
        Guid deviceId,
        string? deviceCode,
        long sequenceNumber,
        string? agentVersion,
        string? clientIp,
        DateTime timestampUtc,
        bool isStatusTransition = false)
    {
        var mode = _heartbeatOptions.CurrentValue.LoggingMode?.Trim() ?? "Normal";
        var count = _heartbeatCounters.AddOrUpdate(deviceId, 1, (_, current) => current + 1);

        // Always log if status transition occurred (reconnected from offline)
        if (isStatusTransition)
        {
            WriteColoredPrefix("[ONLINE]", ConsoleColor.Green,
                $"Heartbeat restored device to ONLINE: '{deviceCode ?? "PC"}' ({deviceId}) | Seq: #{sequenceNumber} | Agent: {agentVersion ?? "1.0"} | IP: {clientIp ?? "LAN"}");
            return;
        }

        if (string.Equals(mode, "Detailed", StringComparison.OrdinalIgnoreCase))
        {
            WriteColoredPrefix("[ONLINE]", ConsoleColor.DarkGreen,
                $"Heartbeat #{sequenceNumber} received: '{deviceCode ?? "PC"}' ({deviceId}) | Agent: {agentVersion ?? "1.0"} | Recv: {timestampUtc:HH:mm:ss.fff} UTC");
        }
        else if (string.Equals(mode, "Normal", StringComparison.OrdinalIgnoreCase))
        {
            // Log periodically: on 1st heartbeat and every 10th heartbeat per device
            if (count == 1 || count % 10 == 0)
            {
                WriteColoredPrefix("[ONLINE]", ConsoleColor.DarkGreen,
                    $"Heartbeat periodic summary [Total: {count}]: '{deviceCode ?? "PC"}' ({deviceId}) | Latest Seq: #{sequenceNumber} | Status: ONLINE");
            }
        }
        // In Quiet mode: heartbeat telemetry is recorded in DB without terminal output unless transition/failure occurs
    }

    public void LogDeviceOffline(
        Guid deviceId,
        string? deviceCode,
        DateTime? lastSeenUtc,
        int timeoutSeconds)
    {
        WriteColoredPrefix("[OFFLINE]", ConsoleColor.Red,
            $"Device Offline Alert: '{deviceCode ?? "PC"}' ({deviceId}) timed out after {timeoutSeconds}s without heartbeat (Last seen: {lastSeenUtc:yyyy-MM-dd HH:mm:ss} UTC). Status: OFFLINE");
    }

    public void LogDeviceReconnected(
        Guid deviceId,
        string? deviceCode,
        string? clientIp)
    {
        WriteColoredPrefix("[ONLINE]", ConsoleColor.Green,
            $"Device Reconnected: '{deviceCode ?? "PC"}' ({deviceId}) returned ONLINE from {clientIp ?? "LAN"}");
    }
}
