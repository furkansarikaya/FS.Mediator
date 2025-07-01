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
/// These extensions provide a fluent API for registering handlers, behaviors, and interceptors
/// with sensible defaults and powerful customization options.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds FS.Mediator services to the specified service collection.
    /// This is the foundation method that sets up the core mediator infrastructure.
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

        // Register handlers and interceptors
        RegisterHandlers(services, assemblies);
        RegisterInterceptors(services, assemblies);

        return services;
    }

    #region Pipeline Behaviors

    /// <summary>
    /// Adds a pipeline behavior to the service collection.
    /// Pipeline behaviors are executed in the order they are registered and provide
    /// cross-cutting concerns like logging, retry logic, and circuit breaking.
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
    /// Think of this as your application's "flight recorder" for debugging and monitoring.
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
        var options = new RetryPolicyOptions();
        configureOptions?.Invoke(options);
        
        services.AddSingleton(options);
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
                options.ShouldRetryPredicate = ex =>
                    ex is HttpRequestException ||
                    ex is TaskCanceledException ||
                    ex is System.Net.Sockets.SocketException ||
                    (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase)) ||
                    (ex.Message.Contains("503", StringComparison.OrdinalIgnoreCase)) ||
                    (ex.Message.Contains("502", StringComparison.OrdinalIgnoreCase));
            }),
            
            _ => services.AddRetryBehavior()
        };
    }
    
    /// <summary>
    /// Adds circuit breaker behavior to the pipeline.
    /// Circuit breaker protects your system from cascade failures by monitoring service health
    /// and temporarily stopping requests to failing services, giving them time to recover.
    /// </summary>
    /// <param name="services">The service collection to add the behavior to.</param>
    /// <param name="configureOptions">Optional configuration action to customize circuit breaker behavior.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCircuitBreakerBehavior(this IServiceCollection services, 
        Action<CircuitBreakerOptions>? configureOptions = null)
    {
        var options = new CircuitBreakerOptions();
        configureOptions?.Invoke(options);
        
        services.AddSingleton(options);
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
                options.FailureThresholdPercentage = 30.0;
                options.MinimumThroughput = 3;
                options.SamplingDuration = TimeSpan.FromSeconds(30);
                options.DurationOfBreak = TimeSpan.FromSeconds(15);
                options.TrialRequestCount = 2;
            }),
            
            CircuitBreakerPreset.Balanced => services.AddCircuitBreakerBehavior(options =>
            {
                options.FailureThresholdPercentage = 50.0;
                options.MinimumThroughput = 5;
                options.SamplingDuration = TimeSpan.FromSeconds(60);
                options.DurationOfBreak = TimeSpan.FromSeconds(30);
                options.TrialRequestCount = 3;
            }),
            
            CircuitBreakerPreset.Resilient => services.AddCircuitBreakerBehavior(options =>
            {
                options.FailureThresholdPercentage = 70.0;
                options.MinimumThroughput = 10;
                options.SamplingDuration = TimeSpan.FromMinutes(2);
                options.DurationOfBreak = TimeSpan.FromMinutes(1);
                options.TrialRequestCount = 5;
            }),
            
            CircuitBreakerPreset.Database => services.AddCircuitBreakerBehavior(options =>
            {
                options.FailureThresholdPercentage = 40.0;
                options.MinimumThroughput = 5;
                options.SamplingDuration = TimeSpan.FromMinutes(1);
                options.DurationOfBreak = TimeSpan.FromSeconds(45);
                options.TrialRequestCount = 2;
                options.ShouldCountAsFailure = ex => !ex.GetType().Name.Contains("Business") && 
                                                     !ex.GetType().Name.Contains("Validation") &&
                                                     ex is not ArgumentException;
            }),
            
            CircuitBreakerPreset.ExternalApi => services.AddCircuitBreakerBehavior(options =>
            {
                options.FailureThresholdPercentage = 60.0;
                options.MinimumThroughput = 8;
                options.SamplingDuration = TimeSpan.FromMinutes(3);
                options.DurationOfBreak = TimeSpan.FromSeconds(60);
                options.TrialRequestCount = 3;
                options.ShouldCountAsFailure = ex => !ex.Message.Contains("400", StringComparison.OrdinalIgnoreCase) &&
                                                     !ex.Message.Contains("401", StringComparison.OrdinalIgnoreCase) &&
                                                     !ex.Message.Contains("403", StringComparison.OrdinalIgnoreCase) &&
                                                     !ex.Message.Contains("404", StringComparison.OrdinalIgnoreCase);
            }),
            
            _ => services.AddCircuitBreakerBehavior()
        };
    }

    /// <summary>
    /// Adds performance monitoring behavior to the pipeline.
    /// This behavior logs warnings for requests that take longer than the specified threshold.
    /// Think of this as your "performance watchdog" that alerts you to slow operations.
    /// </summary>
    /// <param name="services">The service collection to add the behavior to.</param>
    /// <param name="warningThresholdMs">The threshold in milliseconds for logging performance warnings. Default is 500ms.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPerformanceBehavior(this IServiceCollection services, int warningThresholdMs = 500)
    {
        services.AddSingleton(new PerformanceBehaviorOptions { WarningThresholdMs = warningThresholdMs });
        return services.AddPipelineBehavior(typeof(PerformanceBehavior<,>));
    }

    #endregion

    #region Request/Response Interceptors

    /// <summary>
    /// Registers a typed request interceptor for specific request and response types.
    /// Request interceptors execute before the pipeline and are perfect for request validation,
    /// transformation, and security checks. Think of them as "request guards" that ensure
    /// only properly formatted and authorized requests enter your system.
    /// </summary>
    /// <typeparam name="TRequest">The type of request to intercept</typeparam>
    /// <typeparam name="TResponse">The type of response expected</typeparam>
    /// <typeparam name="TInterceptor">The interceptor implementation type</typeparam>
    /// <param name="services">The service collection to register with</param>
    /// <param name="serviceLifetime">The service lifetime (default is Scoped)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddRequestInterceptor<TRequest, TResponse, TInterceptor>(
        this IServiceCollection services,
        ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        where TRequest : IRequest<TResponse>
        where TInterceptor : class, IRequestInterceptor<TRequest, TResponse>
    {
        services.Add(new ServiceDescriptor(
            typeof(IRequestInterceptor<TRequest, TResponse>),
            typeof(TInterceptor),
            serviceLifetime));
        
        return services;
    }

    /// <summary>
    /// Registers a typed request interceptor using a factory function.
    /// This overload is useful when your interceptor needs complex initialization
    /// or depends on runtime configuration that can't be resolved through DI alone.
    /// </summary>
    /// <typeparam name="TRequest">The type of request to intercept</typeparam>
    /// <typeparam name="TResponse">The type of response expected</typeparam>
    /// <typeparam name="TInterceptor">The interceptor implementation type</typeparam>
    /// <param name="services">The service collection to register with</param>
    /// <param name="factory">Factory function to create the interceptor</param>
    /// <param name="serviceLifetime">The service lifetime (default is Scoped)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddRequestInterceptor<TRequest, TResponse, TInterceptor>(
        this IServiceCollection services,
        Func<IServiceProvider, TInterceptor> factory,
        ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        where TRequest : IRequest<TResponse>
        where TInterceptor : class, IRequestInterceptor<TRequest, TResponse>
    {
        services.Add(new ServiceDescriptor(
            typeof(IRequestInterceptor<TRequest, TResponse>),
            factory,
            serviceLifetime));
        
        return services;
    }

    /// <summary>
    /// Registers a typed response interceptor for specific request and response types.
    /// Response interceptors execute after the pipeline completes successfully and are perfect
    /// for response transformation, caching, and enrichment. Think of them as "response
    /// enhancers" that add value to your outgoing data.
    /// </summary>
    /// <typeparam name="TRequest">The type of request that generates the response</typeparam>
    /// <typeparam name="TResponse">The type of response to intercept</typeparam>
    /// <typeparam name="TInterceptor">The interceptor implementation type</typeparam>
    /// <param name="services">The service collection to register with</param>
    /// <param name="serviceLifetime">The service lifetime (default is Scoped)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddResponseInterceptor<TRequest, TResponse, TInterceptor>(
        this IServiceCollection services,
        ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        where TRequest : IRequest<TResponse>
        where TInterceptor : class, IResponseInterceptor<TRequest, TResponse>
    {
        services.Add(new ServiceDescriptor(
            typeof(IResponseInterceptor<TRequest, TResponse>),
            typeof(TInterceptor),
            serviceLifetime));
        
        return services;
    }

    /// <summary>
    /// Registers a typed response interceptor using a factory function.
    /// This overload provides flexibility for complex interceptor initialization scenarios.
    /// </summary>
    /// <typeparam name="TRequest">The type of request that generates the response</typeparam>
    /// <typeparam name="TResponse">The type of response to intercept</typeparam>
    /// <typeparam name="TInterceptor">The interceptor implementation type</typeparam>
    /// <param name="services">The service collection to register with</param>
    /// <param name="factory">Factory function to create the interceptor</param>
    /// <param name="serviceLifetime">The service lifetime (default is Scoped)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddResponseInterceptor<TRequest, TResponse, TInterceptor>(
        this IServiceCollection services,
        Func<IServiceProvider, TInterceptor> factory,
        ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        where TRequest : IRequest<TResponse>
        where TInterceptor : class, IResponseInterceptor<TRequest, TResponse>
    {
        services.Add(new ServiceDescriptor(
            typeof(IResponseInterceptor<TRequest, TResponse>),
            factory,
            serviceLifetime));
        
        return services;
    }

    /// <summary>
    /// Registers a global request interceptor that can process any request type.
    /// Global interceptors are powerful tools for implementing system-wide concerns
    /// like security, auditing, and correlation tracking that apply to all requests.
    /// </summary>
    /// <typeparam name="TInterceptor">The global interceptor implementation type</typeparam>
    /// <param name="services">The service collection to register with</param>
    /// <param name="serviceLifetime">The service lifetime (default is Scoped)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddGlobalRequestInterceptor<TInterceptor>(
        this IServiceCollection services,
        ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        where TInterceptor : class, IGlobalRequestInterceptor
    {
        services.Add(new ServiceDescriptor(
            typeof(IGlobalRequestInterceptor),
            typeof(TInterceptor),
            serviceLifetime));
        
        return services;
    }

    /// <summary>
    /// Registers a global response interceptor that can process any response type.
    /// Global response interceptors are ideal for cross-cutting concerns that need to
    /// affect all outgoing responses, such as adding security headers or performance metrics.
    /// </summary>
    /// <typeparam name="TInterceptor">The global interceptor implementation type</typeparam>
    /// <param name="services">The service collection to register with</param>
    /// <param name="serviceLifetime">The service lifetime (default is Scoped)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddGlobalResponseInterceptor<TInterceptor>(
        this IServiceCollection services,
        ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        where TInterceptor : class, IGlobalResponseInterceptor
    {
        services.Add(new ServiceDescriptor(
            typeof(IGlobalResponseInterceptor),
            typeof(TInterceptor),
            serviceLifetime));
        
        return services;
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Scans assemblies for request handlers, stream handlers, and notification handlers.
    /// This method uses reflection to automatically discover and register all handler types,
    /// saving you from having to manually register each one.
    /// </summary>
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

    /// <summary>
    /// Scans assemblies for interceptor implementations and registers them automatically.
    /// This provides a convention-based approach where any class implementing interceptor
    /// interfaces will be automatically discovered and registered.
    /// </summary>
    private static void RegisterInterceptors(IServiceCollection services, Assembly[] assemblies)
    {
        var interceptorTypes = GetInterceptorTypes(assemblies);

        foreach (var interceptorType in interceptorTypes)
        {
            var interfaces = interceptorType.GetInterfaces()
                .Where(i => i.IsGenericType && 
                           (i.GetGenericTypeDefinition() == typeof(IRequestInterceptor<,>) || 
                            i.GetGenericTypeDefinition() == typeof(IResponseInterceptor<,>)))
                .ToList();

            // Also check for global interceptor interfaces
            var globalInterfaces = interceptorType.GetInterfaces()
                .Where(i => i == typeof(IGlobalRequestInterceptor) || i == typeof(IGlobalResponseInterceptor))
                .ToList();

            // Register typed interceptors
            foreach (var interfaceType in interfaces)
            {
                services.AddScoped(interfaceType, interceptorType);
            }

            // Register global interceptors
            foreach (var interfaceType in globalInterfaces)
            {
                services.AddScoped(interfaceType, interceptorType);
            }
        }
    }

    /// <summary>
    /// Discovers all handler types in the provided assemblies using reflection.
    /// This method looks for classes that implement the core handler interfaces.
    /// </summary>
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

    /// <summary>
    /// Discovers all interceptor types in the provided assemblies using reflection.
    /// This method looks for classes that implement any of the interceptor interfaces.
    /// </summary>
    private static IEnumerable<Type> GetInterceptorTypes(Assembly[] assemblies)
    {
        return assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => type.GetInterfaces()
                .Any(i => (i.IsGenericType && 
                          (i.GetGenericTypeDefinition() == typeof(IRequestInterceptor<,>) ||
                           i.GetGenericTypeDefinition() == typeof(IResponseInterceptor<,>))) ||
                         i == typeof(IGlobalRequestInterceptor) ||
                         i == typeof(IGlobalResponseInterceptor)));
    }

    #endregion
}