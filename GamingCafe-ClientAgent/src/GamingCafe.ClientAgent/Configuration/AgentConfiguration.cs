using System.ComponentModel.DataAnnotations;

namespace GamingCafe.ClientAgent.Configuration;

/// <summary>
/// Strongly-typed configuration options for the Gaming Cafe Client Agent.
/// Contains non-secret settings for REST API, SignalR WSS hub, retry backoff, and heartbeat intervals.
/// Never store credentials, tokens, or private keys in this configuration.
/// </summary>
public class AgentConfiguration
{
    public const string SectionName = "GamingCafeAgent";

    /// <summary>
    /// Base URL for the Admin backend REST API (e.g. https://admin.cafe.lan:5001).
    /// </summary>
    [Required]
    public string BackendApiBaseUrl { get; set; } = "https://127.0.0.1:5001";

    /// <summary>
    /// Endpoint URL for the real-time SignalR Hub (e.g. https://admin.cafe.lan:5001/hubs/agent or wss://...).
    /// </summary>
    [Required]
    public string SignalRHubUrl { get; set; } = "https://127.0.0.1:5001/hubs/agent";

    /// <summary>
    /// Interval in seconds between heartbeat transmissions. Default is 30 seconds.
    /// </summary>
    [Range(5, 3600)]
    public int HeartbeatIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// HTTP request timeout in seconds for REST API calls.
    /// </summary>
    [Range(2, 120)]
    public int HttpTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Handshake and connection timeout in seconds for SignalR.
    /// </summary>
    [Range(5, 120)]
    public int SignalRConnectionTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Polling interval in seconds when device enrollment is in PendingApproval state.
    /// </summary>
    [Range(3, 600)]
    public int EnrollmentPollIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Minimum retry backoff delay in seconds.
    /// </summary>
    [Range(1, 60)]
    public int MinRetryBackoffSeconds { get; set; } = 2;

    /// <summary>
    /// Maximum retry backoff delay in seconds.
    /// </summary>
    [Range(5, 1800)]
    public int MaxRetryBackoffSeconds { get; set; } = 60;

    /// <summary>
    /// Cryptographic jitter factor (between 0.0 and 0.5) applied to retry delays.
    /// </summary>
    [Range(0.0, 0.5)]
    public double BackoffJitterFactor { get; set; } = 0.2;

    /// <summary>
    /// Client Agent semantic version string.
    /// </summary>
    public string AgentVersion { get; set; } = "1.0.0-phase1";

    /// <summary>
    /// Message exchange protocol version.
    /// </summary>
    public string ProtocolVersion { get; set; } = "1.0";

    /// <summary>
    /// Optional custom data directory for identity and encrypted storage.
    /// Defaults to %ProgramData%\GamingCafe\ClientAgent on Windows.
    /// </summary>
    public string? LocalDataDirectory { get; set; }

    /// <summary>
    /// Hosting environment name ("Production" or "Development").
    /// </summary>
    public string EnvironmentName { get; set; } = "Production";

    /// <summary>
    /// TLS validation strategy: "SystemTrust" (standard CA store) or "PinnedThumbprint".
    /// </summary>
    public string TlsValidationMode { get; set; } = "SystemTrust";

    /// <summary>
    /// Pinned SHA-256 certificate thumbprint if using self-signed certificate on cafe LAN.
    /// </summary>
    public string? PinnedCertificateThumbprint { get; set; }
}
