using System.Text.RegularExpressions;

namespace TrustRent.Shared.Security;

public static class UserAgentHelper
{
    /// <summary>
    /// Truncates a full User-Agent string to just the browser family + version and OS,
    /// reducing PII fingerprinting surface while keeping useful diagnostic info.
    /// Example: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/125.0.0.0 Safari/537.36"
    /// becomes "Chrome 125 / Windows"
    /// </summary>
    public static string? Simplify(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return null;

        var browser = ParseBrowser(userAgent);
        var os = ParseOs(userAgent);

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(browser)) parts.Add(browser);
        if (!string.IsNullOrEmpty(os)) parts.Add(os);

        return parts.Count > 0 ? string.Join(" / ", parts) : "Unknown";
    }

    private static string? ParseBrowser(string ua)
    {
        // Order matters — check Edge/Opera before Chrome/Safari
        var match = Regex.Match(ua, @"Edg/(\d+)", RegexOptions.IgnoreCase);
        if (match.Success) return $"Edge {match.Groups[1].Value}";

        match = Regex.Match(ua, @"OPR/(\d+)", RegexOptions.IgnoreCase);
        if (match.Success) return $"Opera {match.Groups[1].Value}";

        match = Regex.Match(ua, @"Chrome/(\d+)", RegexOptions.IgnoreCase);
        if (match.Success) return $"Chrome {match.Groups[1].Value}";

        match = Regex.Match(ua, @"Firefox/(\d+)", RegexOptions.IgnoreCase);
        if (match.Success) return $"Firefox {match.Groups[1].Value}";

        match = Regex.Match(ua, @"Version/(\d+).*Safari", RegexOptions.IgnoreCase);
        if (match.Success) return $"Safari {match.Groups[1].Value}";

        return null;
    }

    private static string? ParseOs(string ua)
    {
        if (Regex.IsMatch(ua, @"Windows NT 10", RegexOptions.IgnoreCase)) return "Windows";
        if (Regex.IsMatch(ua, @"Windows NT", RegexOptions.IgnoreCase)) return "Windows";
        if (Regex.IsMatch(ua, @"Android", RegexOptions.IgnoreCase)) return "Android";
        if (Regex.IsMatch(ua, @"iPhone|iPad|iPod", RegexOptions.IgnoreCase)) return "iOS";
        if (Regex.IsMatch(ua, @"Mac OS X", RegexOptions.IgnoreCase)) return "macOS";
        if (Regex.IsMatch(ua, @"Linux", RegexOptions.IgnoreCase)) return "Linux";
        return null;
    }
}
