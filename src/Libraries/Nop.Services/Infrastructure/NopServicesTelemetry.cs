using System.Diagnostics;

namespace Nop.Services.Infrastructure;

public static class NopServicesTelemetry
{
    public const string ActivitySourceName = "NopCommerce.Services";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
