using System.Diagnostics.Metrics;

namespace Nop.Web.Infrastructure.Telemetry;

public static class NopMetrics
{
    public const string MeterName = "NopCommerce.Operations";

    public static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> SearchPerformed =
        Meter.CreateCounter<long>("search_performed", description: "Total number of search queries submitted");

    public static readonly Counter<long> SearchNoResults =
        Meter.CreateCounter<long>("search_no_results", description: "Number of search queries that returned zero products");

}
