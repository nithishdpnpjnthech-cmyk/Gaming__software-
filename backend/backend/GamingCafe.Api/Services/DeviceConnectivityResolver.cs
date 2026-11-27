using GamingCafe.Api.Configuration;

namespace GamingCafe.Api.Services;

public static class DeviceConnectivityResolver
{
    public static string Resolve(
        string registrationStatus,
        DateTime? lastSeenAt,
        DateTime now,
        HeartbeatOptions options)
    {
        if (registrationStatus != "APPROVED")
        {
            return registrationStatus;
        }

        if (!lastSeenAt.HasValue)
        {
            return "NEVER_CONNECTED";
        }

        return options.IsOnline(lastSeenAt, now)
            ? "ONLINE"
            : "OFFLINE";
    }
}