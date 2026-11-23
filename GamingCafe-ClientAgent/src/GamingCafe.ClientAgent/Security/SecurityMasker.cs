using System.Text.RegularExpressions;

namespace GamingCafe.ClientAgent.Security;

/// <summary>
/// Defensive sanitization helper to guarantee secrets, tokens, and authorization headers
/// are strictly redacted prior to logging or diagnostic output.
/// </summary>
public static class SecurityMasker
{
    private const string Mask = "[REDACTED]";

    public static string MaskSecret(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return "<empty>";
        }

        if (secret.Length <= 8)
        {
            return "******";
        }

        return $"{secret[..3]}...{secret[^3..]}";
    }

    public static string SanitizeLogMessage(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        // Redact HTTP Bearer tokens
        var sanitized = Regex.Replace(input, @"(Bearer\s+)[A-Za-z0-9_\-\.]+", $"$1{Mask}", RegexOptions.IgnoreCase);

        // Redact JSON fields containing sensitive credential strings
        sanitized = Regex.Replace(sanitized, @"([""']?(?:deviceSecret|token|password|secret|authorization)[""']?\s*:\s*[""'])(?:[^""'\\]|\\.)*([""'])", $"$1{Mask}$2", RegexOptions.IgnoreCase);

        return sanitized;
    }
}
