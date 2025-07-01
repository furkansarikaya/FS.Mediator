using System.Reflection;
using FS.Mediator.Behaviors;
using FS.Mediator.Core;
using FS.Mediator.Implementation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FS.Mediator.Extensions;

/// <summary>
/// Extension methods for configuring FS.Mediator services in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds FS.Mediator services to the specified service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="assemblies">The assemblies to scan for handlers. If none provided, scans the calling assembly.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFSMediator(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
        {
            assemblies = [Assembly.GetCallingAssembly()];
        }

        // Register core services
        services.TryAddScoped<IMediator, Implementation.Mediator>();
        services.TryAddScoped<ServiceFactory>(provider => provider.GetRequiredService);
        services.TryAddScoped<ServiceFactoryCollection>(provider => 
            serviceType => provider.GetServices(serviceType)!);

        // Register handlers
        RegisterHandlers(services, assemblies);

        return services;
    }

    /// <summary>
    /// Adds a pipeline behavior to the service collection.
    /// Pipeline behaviors are executed in the order they are registered.
    /// </summary>
    /// <param name="services">The service collection to add the behavior to.</param>
    /// <param name="behaviorType">The type of behavior to add.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPipelineBehavior(this IServiceCollection services, Type behaviorType)
    {
        services.AddTransient(typeof(IPipelineBehavior<,>), behaviorType);
        return services;
    }

    /// <summary>
    /// Adds logging behavior to the pipeline.
    /// This behavior logs request processing information including execution time and errors.
    /// </summary>
    /// <param name="services">The service collection to add the behavior to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLoggingBehavior(this IServiceCollection services)
    {
        return services.AddPipelineBehavior(typeof(LoggingBehavior<,>));
    }

    /// <summary>
    /// Adds performance monitoring behavior to the pipeline.
    /// This behavior logs warnings for requests that take longer than the specified threshold.
    /// </summary>
    /// <param name="services">The service collection to add the behavior to.</param>
    /// <param name="warningThresholdMs">The threshold in milliseconds for logging performance warnings. Default is 500ms.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPerformanceBehavior(this IServiceCollection services, int warningThresholdMs = 500)
    {
        // Register the warning threshold as a singleton value
        services.AddSingleton(new PerformanceBehaviorOptions { WarningThresholdMs = warningThresholdMs });
        
        // Register the behavior
        return services.AddPipelineBehavior(typeof(PerformanceBehavior<,>));
    }

    private static void RegisterHandlers(IServiceCollection services, Assembly[] assemblies)
    {
        var handlerTypes = GetHandlerTypes(assemblies);

        foreach (var handlerType in handlerTypes)
        {
            var interfaces = handlerType.GetInterfaces()
                .Where(i => i.IsGenericType && 
                           (i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) || 
                            i.GetGenericTypeDefinition() == typeof(IStreamRequestHandler<,>) ||
                            i.GetGenericTypeDefinition() == typeof(INotificationHandler<>)))
                .ToList();

            foreach (var interfaceType in interfaces)
            {
                services.AddScoped(interfaceType, handlerType);
            }
        }
    }

    private static IEnumerable<Type> GetHandlerTypes(Assembly[] assemblies)
    {
        return assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => type.GetInterfaces()
                .Any(i => i.IsGenericType && 
                         (i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) ||
                          i.GetGenericTypeDefinition() == typeof(IStreamRequestHandler<,>) ||
                          i.GetGenericTypeDefinition() == typeof(INotificationHandler<>))));
    }
}