using System.Diagnostics;

namespace Nop.Web.Infrastructure.Telemetry;

public static class NopTelemetry
{
    public const string ActivitySourceName = "NopCommerce.Catalog";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
