using System.Reflection;
using FS.Mediator.Behaviors;
using FS.Mediator.Core;
using FS.Mediator.Implementation;
using FS.Mediator.Models.Enums;
using FS.Mediator.Models.Options;
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
    /// Adds retry policy behavior to the pipeline.
    /// This behavior automatically retries failed requests based on configurable strategies,
    /// significantly improving your application's resilience to transient failures.
    /// 
    /// Retry policies are particularly valuable in distributed systems where temporary
    /// network issues, database timeouts, or service overloads can cause requests to fail
    /// even though the underlying operation could succeed if attempted again.
    /// </summary>
    /// <param name="services">The service collection to add the behavior to.</param>
    /// <param name="configureOptions">Optional configuration action to customize retry behavior.
    /// If not provided, sensible defaults are used (3 retries with exponential backoff).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRetryBehavior(this IServiceCollection services, 
        Action<RetryPolicyOptions>? configureOptions = null)
    {
        // Create default options and allow customization
        var options = new RetryPolicyOptions();
        configureOptions?.Invoke(options);
        
        // Register the configured options as a singleton
        services.AddSingleton(options);
        
        // Register the retry behavior in the pipeline
        return services.AddPipelineBehavior(typeof(RetryBehavior<,>));
    }
    
    /// <summary>
    /// Adds retry policy behavior with predefined configuration for common scenarios.
    /// This method provides several preset configurations that work well for typical use cases,
    /// saving you from having to understand all the retry theory upfront.
    /// </summary>
    /// <param name="services">The service collection to add the behavior to.</param>
    /// <param name="preset">The preset configuration to use</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRetryBehavior(this IServiceCollection services, RetryPreset preset)
    {
        return preset switch
        {
            RetryPreset.Conservative => services.AddRetryBehavior(options =>
            {
                options.MaxRetryAttempts = 2;
                options.InitialDelay = TimeSpan.FromMilliseconds(500);
                options.Strategy = RetryStrategy.FixedDelay;
                options.MaxTotalRetryTime = TimeSpan.FromSeconds(10);
            }),
            
            RetryPreset.Aggressive => services.AddRetryBehavior(options =>
            {
                options.MaxRetryAttempts = 5;
                options.InitialDelay = TimeSpan.FromMilliseconds(200);
                options.Strategy = RetryStrategy.ExponentialBackoffWithJitter;
                options.MaxTotalRetryTime = TimeSpan.FromMinutes(2);
            }),
            
            RetryPreset.Database => services.AddRetryBehavior(options =>
            {
                options.MaxRetryAttempts = 3;
                options.InitialDelay = TimeSpan.FromSeconds(1);
                options.Strategy = RetryStrategy.ExponentialBackoff;
                options.MaxTotalRetryTime = TimeSpan.FromSeconds(30);
                // Custom predicate for database-specific exceptions
                options.ShouldRetryPredicate = ex => 
                    ex.GetType().Name.Contains("Timeout") ||
                    ex.GetType().Name.Contains("Connection") ||
                    ex.GetType().Name.Contains("DeadLock") ||
                    ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase);
            }),
            
            RetryPreset.HttpApi => services.AddRetryBehavior(options =>
            {
                options.MaxRetryAttempts = 4;
                options.InitialDelay = TimeSpan.FromMilliseconds(750);
                options.Strategy = RetryStrategy.ExponentialBackoffWithJitter;
                options.MaxTotalRetryTime = TimeSpan.FromSeconds(45);
                // Custom predicate for HTTP-specific scenarios
                options.ShouldRetryPredicate = ex =>
                    ex is System.Net.Http.HttpRequestException ||
                    ex is TaskCanceledException ||
                    ex is System.Net.Sockets.SocketException ||
                    (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase)) ||
                    (ex.Message.Contains("503", StringComparison.OrdinalIgnoreCase)) ||
                    (ex.Message.Contains("502", StringComparison.OrdinalIgnoreCase));
            }),
            
            _ => services.AddRetryBehavior() // Default configuration
        };
    }
    
    /// <summary>
    /// Adds circuit breaker behavior to the pipeline.
    /// Circuit breaker protects your system from cascade failures by monitoring service health
    /// and temporarily stopping requests to failing services, giving them time to recover.
    /// 
    /// This is particularly valuable in distributed systems where one failing dependency
    /// can bring down your entire application if not properly isolated.
    /// </summary>
    /// <param name="services">The service collection to add the behavior to.</param>
    /// <param name="configureOptions">Optional configuration action to customize circuit breaker behavior.
    /// If not provided, sensible defaults are used (50% failure threshold, 60s sampling window).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCircuitBreakerBehavior(this IServiceCollection services, 
        Action<CircuitBreakerOptions>? configureOptions = null)
    {
        // Create default options and allow customization
        var options = new CircuitBreakerOptions();
        configureOptions?.Invoke(options);
        
        // Register the configured options as a singleton
        services.AddSingleton(options);
        
        // Register the circuit breaker behavior in the pipeline
        return services.AddPipelineBehavior(typeof(CircuitBreakerBehavior<,>));
    }

    /// <summary>
    /// Adds circuit breaker behavior with predefined configuration for common scenarios.
    /// These presets represent battle-tested configurations optimized for different types of services.
    /// </summary>
    /// <param name="services">The service collection to add the behavior to.</param>
    /// <param name="preset">The preset configuration to use</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCircuitBreakerBehavior(this IServiceCollection services, CircuitBreakerPreset preset)
    {
        return preset switch
        {
            CircuitBreakerPreset.Sensitive => services.AddCircuitBreakerBehavior(options =>
            {
                options.FailureThresholdPercentage = 30.0; // Trip at 30% failure rate
                options.MinimumThroughput = 3; // Need only 3 requests to make decision
                options.SamplingDuration = TimeSpan.FromSeconds(30); // Short memory for quick response
                options.DurationOfBreak = TimeSpan.FromSeconds(15); // Quick recovery attempts
                options.TrialRequestCount = 2; // Fewer trial requests
            }),
            
            CircuitBreakerPreset.Balanced => services.AddCircuitBreakerBehavior(options =>
            {
                options.FailureThresholdPercentage = 50.0; // Standard 50% threshold
                options.MinimumThroughput = 5; // Reasonable sample size
                options.SamplingDuration = TimeSpan.FromSeconds(60); // Standard monitoring window
                options.DurationOfBreak = TimeSpan.FromSeconds(30); // Standard recovery time
                options.TrialRequestCount = 3; // Standard trial count
            }),
            
            CircuitBreakerPreset.Resilient => services.AddCircuitBreakerBehavior(options =>
            {
                options.FailureThresholdPercentage = 70.0; // Higher tolerance for failures
                options.MinimumThroughput = 10; // Need more data before tripping
                options.SamplingDuration = TimeSpan.FromMinutes(2); // Longer observation period
                options.DurationOfBreak = TimeSpan.FromMinutes(1); // Longer recovery time
                options.TrialRequestCount = 5; // More trial requests for confidence
            }),
            
            CircuitBreakerPreset.Database => services.AddCircuitBreakerBehavior(options =>
            {
                options.FailureThresholdPercentage = 40.0; // Databases are critical, trip early
                options.MinimumThroughput = 5;
                options.SamplingDuration = TimeSpan.FromMinutes(1);
                options.DurationOfBreak = TimeSpan.FromSeconds(45); // Give DB time to recover
                options.TrialRequestCount = 2; // Conservative testing
                // Custom predicate for database-specific failures
                options.ShouldCountAsFailure = ex => !ex.GetType().Name.Contains("Business") && 
                                                     !ex.GetType().Name.Contains("Validation") &&
                                                     ex is not ArgumentException;
            }),
            
            CircuitBreakerPreset.ExternalApi => services.AddCircuitBreakerBehavior(options =>
            {
                options.FailureThresholdPercentage = 60.0; // External APIs can be flaky
                options.MinimumThroughput = 8;
                options.SamplingDuration = TimeSpan.FromMinutes(3); // Longer window for external services
                options.DurationOfBreak = TimeSpan.FromSeconds(60); // Give external service time
                options.TrialRequestCount = 3;
                // Custom predicate for HTTP-specific scenarios
                options.ShouldCountAsFailure = ex => !ex.Message.Contains("400", StringComparison.OrdinalIgnoreCase) &&
                                                     !ex.Message.Contains("401", StringComparison.OrdinalIgnoreCase) &&
                                                     !ex.Message.Contains("403", StringComparison.OrdinalIgnoreCase) &&
                                                     !ex.Message.Contains("404", StringComparison.OrdinalIgnoreCase);
            }),
            
            _ => services.AddCircuitBreakerBehavior() // Default configuration
        };
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