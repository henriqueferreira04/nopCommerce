using System.Diagnostics.Metrics;

namespace Nop.Services.Infrastructure;

public static class NopServicesMetrics
{
    public static readonly Meter Meter = new("NopCommerce.Operations");

    public static readonly Counter<long> PricingCacheMiss =
        Meter.CreateCounter<long>("pricing_cache_miss",
            description: "Number of pricing cache misses in GetFinalPrice");
}
