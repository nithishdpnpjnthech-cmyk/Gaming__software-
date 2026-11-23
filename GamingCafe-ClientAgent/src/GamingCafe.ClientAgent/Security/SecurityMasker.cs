using System.Text.RegularExpressions;

namespace GamingCafe.ClientAgent.Security;

public static partial class SecurityMasker
{
    [GeneratedRegex(@"(?i)(token|bearer|secret|password|key)\s*[:=]\s*([a-zA-Z0-9_\-\.]{6,})")]
    private static partial Regex SecretPatternRegex();

    public static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length <= 8) return "***";

        return $"{value[..4]}...{value[^4..]}";
    }

    public static string SanitizeLogMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return string.Empty;

        return SecretPatternRegex().Replace(message, match =>
        {
            var key = match.Groups[1].Value;
            var val = match.Groups[2].Value;
            return $"{key}:{Mask(val)}";
        });
    }
}
