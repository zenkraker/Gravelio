namespace Gravelio.Api.Sessions;

/// A short "Browser on System" label for the session list. Plain substring checks: it only has to help a person recognise a device.
public static class UserAgentInfo
{
    public static string Describe(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "Unknown device";
        var browser = userAgent switch
        {
            _ when userAgent.Contains("Edg/") => "Edge",
            _ when userAgent.Contains("OPR/") => "Opera",
            _ when userAgent.Contains("Firefox/") => "Firefox",
            _ when userAgent.Contains("Chrome/") || userAgent.Contains("CriOS/") => "Chrome",
            _ when userAgent.Contains("Safari/") => "Safari",
            _ => "Browser",
        };
        var system = userAgent switch
        {
            _ when userAgent.Contains("Android") => "Android",
            _ when userAgent.Contains("iPhone") || userAgent.Contains("iPad") => "iOS",
            _ when userAgent.Contains("Windows") => "Windows",
            _ when userAgent.Contains("Mac OS X") => "macOS",
            _ when userAgent.Contains("Linux") => "Linux",
            _ => null,
        };
        return system is null ? browser : $"{browser} on {system}";
    }
}
