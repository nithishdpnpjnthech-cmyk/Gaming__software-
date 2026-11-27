namespace GamingCafe.Api.Configuration;
public sealed class HeartbeatOptions
{
    public int IntervalSeconds { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 15;
    public string LoggingMode { get; set; } = "Normal"; // "Detailed", "Normal", "Quiet"
    public bool IsOnline(DateTime? lastSeenUtc, DateTime nowUtc) =>
        lastSeenUtc.HasValue && nowUtc - lastSeenUtc.Value < TimeSpan.FromSeconds(TimeoutSeconds);
}
