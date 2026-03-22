using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Nop.Web.Framework.Infrastructure;

/// <summary>
/// Middleware that creates a trace span for every MVC controller action.
/// Span name format: "{Controller}.{Action}" (e.g., "Catalog.Search", "Product.ProductDetails").
/// Skips non-MVC requests (static files, health checks, etc.).
/// </summary>
public class TracingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ActivitySource _activitySource;

    public TracingMiddleware(RequestDelegate next, ActivitySource activitySource)
    {
        _next = next;
        _activitySource = activitySource;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var controller = context.Request.RouteValues["controller"]?.ToString();
        var action = context.Request.RouteValues["action"]?.ToString();

        if (controller is null || action is null)
        {
            await _next(context);
            return;
        }

        using var activity = _activitySource.StartActivity($"{controller}.{action}");

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
