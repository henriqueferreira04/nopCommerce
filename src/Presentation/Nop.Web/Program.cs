using Autofac.Extensions.DependencyInjection;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Web.Framework.Infrastructure.Extensions;
using Nop.Services.Infrastructure;
using Nop.Web.Infrastructure.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Nop.Web;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Configuration.AddJsonFile(NopConfigurationDefaults.AppSettingsFilePath, true, true);
        if (!string.IsNullOrEmpty(builder.Environment?.EnvironmentName))
        {
            var path = string.Format(NopConfigurationDefaults.AppSettingsEnvironmentFilePath, builder.Environment.EnvironmentName);
            builder.Configuration.AddJsonFile(path, true, true);
        }
        builder.Configuration.AddEnvironmentVariables();

        //load application settings
        builder.Services.ConfigureApplicationSettings(builder);

        var appSettings = Singleton<AppSettings>.Instance;
        var useAutofac = appSettings.Get<CommonConfig>().UseAutofac;

        if (useAutofac)
            builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
        else
        {
            builder.Host.UseDefaultServiceProvider(options =>
            {
                //we don't validate the scopes, since at the app start and the initial configuration we need 
                //to resolve some services (registered as "scoped") through the root container
                options.ValidateScopes = false;
                options.ValidateOnBuild = true;
            });
        }

        //add services to the application and configure service provider
        builder.Services.ConfigureApplicationServices(builder);

        var otlpEndpoint = builder.Configuration["Otlp:Endpoint"]
            ?? throw new InvalidOperationException("Otlp:Endpoint configuration is required. Set it via environment variable Otlp__Endpoint.");

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("nopCommerce"))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(opts =>
                    {
                        opts.EnrichWithHttpRequest = (activity, request) =>
                        {
                            var route = (request.HttpContext.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
                            activity.DisplayName = $"{request.Method} {route ?? request.Path.Value}";
                        };
                    })
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation(options => options.SetDbStatementForText = true)
                    .AddSource(NopTelemetry.ActivitySourceName)
                    .AddSource(NopServicesTelemetry.ActivitySourceName)
                    .AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint));
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(NopMetrics.MeterName)
                    .AddOtlpExporter((opts, metricOpts) =>
                    {
                        opts.Endpoint = new Uri(otlpEndpoint);
                        metricOpts.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 20_000;
                    });
            });

        var app = builder.Build();

        //configure the application HTTP request pipeline
        app.ConfigureRequestPipeline();
        await app.PublishAppStartedEventAsync();

        await app.RunAsync();
    }
}