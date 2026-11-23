using System.ComponentModel.DataAnnotations;

namespace GamingCafe.ClientAgent.Configuration;

/// <summary>
/// Strongly-typed configuration options for the Gaming PC Client Agent.
/// Never contains passwords, private keys, or tokens.
/// </summary>
public class AgentOptions
{
    public const string SectionName = "GamingCafeAgent";

    /// <summary>
    /// Base URL of the Admin PC backend API (e.g. https://admin.lan:5001 or https://192.168.1.100:5001).
    /// </summary>
    [Required]
    public string AdminApiBaseUrl { get; set; } = "https://127.0.0.1:5001";

    /// <summary>
    /// Heartbeat interval in seconds.
    /// </summary>
    [Range(5, 3600)]
    public int HeartbeatIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// HTTP request timeout in seconds.
    /// </summary>
    [Range(2, 120)]
    public int ConnectionTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Poll interval in seconds when device is pending admin approval.
    /// </summary>
    [Range(3, 600)]
    public int EnrollmentPollIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Minimum retry backoff interval in seconds.
    /// </summary>
    [Range(1, 60)]
    public int MinRetryBackoffSeconds { get; set; } = 2;

    /// <summary>
    /// Maximum retry backoff interval in seconds.
    /// </summary>
    [Range(5, 1800)]
    public int MaxRetryBackoffSeconds { get; set; } = 60;

    /// <summary>
    /// Jitter factor between 0.0 and 0.5 to prevent synchronized thundering herd.
    /// </summary>
    [Range(0.0, 0.5)]
    public double BackoffJitterFactor { get; set; } = 0.2;

    /// <summary>
    /// Agent software version.
    /// </summary>
    public string AgentVersion { get; set; } = "1.0.0-phase1";

    /// <summary>
    /// Protocol version for message exchange.
    /// </summary>
    public string ProtocolVersion { get; set; } = "1.0";

    /// <summary>
    /// Local directory where identity and encrypted credentials are stored.
    /// If empty, defaults to %ProgramData%\GamingCafe\ClientAgent on Windows.
    /// </summary>
    public string? LocalDataDirectory { get; set; }

    /// <summary>
    /// Environment name (Development or Production).
    /// </summary>
    public string EnvironmentName { get; set; } = "Production";

    /// <summary>
    /// TLS certificate validation policy: "SystemTrust" (default, strict CA check) or "PinnedThumbprint".
    /// Note: Relaxed or disabled validation is strictly disallowed.
    /// </summary>
    public string TlsValidationMode { get; set; } = "SystemTrust";

    /// <summary>
    /// Optional SHA-256 certificate thumbprint if using pinned server certificate in cafe LAN.
    /// </summary>
    public string? PinnedCertificateThumbprint { get; set; }
}
