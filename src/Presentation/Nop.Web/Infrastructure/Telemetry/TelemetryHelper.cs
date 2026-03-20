namespace Nop.Web.Infrastructure.Telemetry;

public static class TelemetryHelper
{
    public static string GetDeviceType(string userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return "unknown";

        // Check tablet before mobile — Android without "Mobile" is a tablet
        if (userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase) ||
            (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase) &&
             !userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase)) ||
            userAgent.Contains("Tablet", StringComparison.OrdinalIgnoreCase))
            return "tablet";

        if (userAgent.Contains("Mobi", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("iPod", StringComparison.OrdinalIgnoreCase))
            return "mobile";

        return "desktop";
    }
}
