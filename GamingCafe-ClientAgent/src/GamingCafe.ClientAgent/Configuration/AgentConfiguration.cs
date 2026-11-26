namespace GamingCafe.ClientAgent.Configuration;

public sealed class AgentConfiguration
{
    public BackendConfig Backend { get; set; } = new();
    public ReconnectionConfig Reconnection { get; set; } = new();
    public HeartbeatConfig Heartbeat { get; set; } = new();
    public IdentityConfig Identity { get; set; } = new();
    public SecurityConfig Security { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();
}

public sealed class BackendConfig
{
    public string ApiBaseUrl { get; set; } = "http://localhost:5022";
    public string HubUrl { get; set; } = "wss://localhost:5001/hubs/agent";
    public bool EnableSignalR { get; set; } = false;
    public bool RequireWss { get; set; } = true;
    public bool ValidateCertificates { get; set; } = true;
    public string ExpectedCertificateThumbprint { get; set; } = string.Empty;
    public int ConnectionTimeoutSeconds { get; set; } = 30;
    public int HandshakeTimeoutSeconds { get; set; } = 15;
    public int HttpTimeoutSeconds { get; set; } = 15;
}

public sealed class ReconnectionConfig
{
    public double InitialDelaySeconds { get; set; } = 2.0;
    public double MaxDelaySeconds { get; set; } = 60.0;
    public double BackoffMultiplier { get; set; } = 2.0;
    public double JitterMaxSeconds { get; set; } = 3.0;
    public int MaxReconnectAttempts { get; set; } = 0; // 0 = infinite
}

public sealed class HeartbeatConfig
{
    public int IntervalSeconds { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 2;
    public int MaxMissedHeartbeats { get; set; } = 3;
}

public sealed class IdentityConfig
{
    public string StorageDirectory { get; set; } = "data";
    public string ClientVersion { get; set; } = "1.0.0";
}

public sealed class SecurityConfig
{
    public bool UseMachineScope { get; set; } = true;
    public bool RejectNonWss { get; set; } = true;
}

public sealed class LoggingConfig
{
    public FileLogConfig File { get; set; } = new();
}

public sealed class FileLogConfig
{
    public string LogDirectory { get; set; } = "logs";
    public int MaxFileSizeMb { get; set; } = 10;
    public int RetainedFileDays { get; set; } = 14;
}
