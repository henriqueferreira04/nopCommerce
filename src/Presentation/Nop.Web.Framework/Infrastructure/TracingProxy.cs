using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Nop.Web.Framework.Infrastructure;

/// <summary>
/// Registers TracingProxy decoration on existing service registrations.
/// </summary>
public static class TracingProxy
{
    private static readonly MethodInfo _createProxyMethod = typeof(TracingProxy)
        .GetMethod(nameof(CreateProxy), BindingFlags.NonPublic | BindingFlags.Static);

    /// <summary>
    /// Wraps existing service registrations with TracingProxy so every interface method call
    /// creates a trace span. Existing registrations stay untouched — they are re-registered
    /// under their implementation type, and the interface resolves via a proxy wrapper.
    /// </summary>
    public static void AddTracing(IServiceCollection services, ActivitySource activitySource, params Type[] serviceInterfaces)
    {
        foreach (var serviceType in serviceInterfaces)
        {
            var descriptor = FindDescriptor(services, serviceType);
            if (descriptor?.ImplementationType == null)
                continue;

            var implType = descriptor.ImplementationType;
            var lifetime = descriptor.Lifetime;
            var serviceName = serviceType.Name.StartsWith('I')
                ? serviceType.Name[1..]
                : serviceType.Name;

            services.Remove(descriptor);
            services.TryAdd(new ServiceDescriptor(implType, implType, lifetime));
            services.Add(new ServiceDescriptor(
                serviceType,
                sp =>
                {
                    var target = sp.GetRequiredService(implType);
                    return _createProxyMethod
                        .MakeGenericMethod(serviceType)
                        .Invoke(null, [target, activitySource, serviceName]);
                },
                lifetime));
        }
    }

    private static ServiceDescriptor FindDescriptor(IServiceCollection services, Type serviceType)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == serviceType)
                return services[i];
        }
        return null;
    }

    private static TInterface CreateProxy<TInterface>(object target, ActivitySource activitySource, string serviceName)
        where TInterface : class
    {
        return TracingProxy<TInterface>.Wrap((TInterface)target, activitySource, serviceName);
    }
}

/// <summary>
/// A DispatchProxy that automatically creates trace spans for every method call on a wrapped interface.
/// Used at the DI registration level to instrument service/factory interfaces without modifying business logic.
/// </summary>
public class TracingProxy<TInterface> : DispatchProxy where TInterface : class
{
    private TInterface _target;
    private ActivitySource _activitySource;
    private string _serviceName;

    /// <summary>
    /// Wraps an existing service instance with tracing.
    /// </summary>
    public static TInterface Wrap(TInterface target, ActivitySource activitySource, string serviceName)
    {
        var proxy = Create<TInterface, TracingProxy<TInterface>>();
        var tracingProxy = proxy as TracingProxy<TInterface>;
        tracingProxy._target = target;
        tracingProxy._activitySource = activitySource;
        tracingProxy._serviceName = serviceName;
        return proxy;
    }

    protected override object Invoke(MethodInfo targetMethod, object[] args)
    {
        var activity = _activitySource.StartActivity($"{_serviceName}.{targetMethod.Name}");

        try
        {
            var result = targetMethod.Invoke(_target, args);

            // Handle async methods (Task and Task<T>)
            if (result is Task task)
            {
                return WrapAsync(task, activity, targetMethod.ReturnType);
            }

            activity?.Stop();
            activity?.Dispose();
            return result;
        }
        catch (TargetInvocationException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.InnerException?.Message);
            activity?.Stop();
            activity?.Dispose();
            throw ex.InnerException ?? ex;
        }
    }

    /// <summary>
    /// Wraps a Task so the activity stays alive until the async work completes.
    /// </summary>
    private static object WrapAsync(Task task, Activity activity, Type returnType)
    {
        // For Task (no result)
        if (returnType == typeof(Task))
        {
            return WrapTask(task, activity);
        }

        // For Task<T> — need to preserve the generic return type
        var resultType = returnType.GetGenericArguments()[0];
        var method = typeof(TracingProxy<TInterface>)
            .GetMethod(nameof(WrapTaskOfT), BindingFlags.NonPublic | BindingFlags.Static)
            .MakeGenericMethod(resultType);

        return method.Invoke(null, new object[] { task, activity });
    }

    private static async Task WrapTask(Task task, Activity activity)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            activity?.Stop();
            activity?.Dispose();
        }
    }

    private static async Task<T> WrapTaskOfT<T>(Task<T> task, Activity activity)
    {
        try
        {
            return await task;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            activity?.Stop();
            activity?.Dispose();
        }
    }
}
