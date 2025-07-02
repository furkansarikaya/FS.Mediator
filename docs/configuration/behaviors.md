# Pipeline Behaviors Configuration Guide

Welcome to the comprehensive guide for configuring pipeline behaviors in FS.Mediator! This is where we move from using the library's defaults to crafting a precisely tuned system that matches your application's specific needs. Think of this as learning to be a skilled conductor of an orchestra - you're not just playing music, you're coordinating multiple instruments to create exactly the symphony you envision.

Pipeline behaviors are the secret sauce that makes FS.Mediator so powerful. They're like having a team of specialists who each add their expertise to every request that flows through your system. One specialist handles logging, another manages retries, yet another protects against failures - all working together seamlessly without cluttering your business logic.

## Understanding the Pipeline Architecture

Before diving into specific configurations, let's understand how the pipeline works. When you send a request through FS.Mediator, it doesn't go directly to your handler. Instead, it flows through a carefully orchestrated pipeline of behaviors, each adding value along the way.

Imagine your request as a package being processed through a sophisticated mail sorting facility. Each station in the facility (behavior) has a specific job - one station weighs packages, another checks addresses, another applies postage, and so on. The package emerges at the end properly processed and ready for delivery.

```csharp
// This is what happens when you send a request
var result = await _mediator.SendAsync(new GetUserQuery(123));

// Behind the scenes, the request flows through this pipeline:
// Request → Logging → Performance → Retry → Circuit Breaker → Your Handler → Response
//        ←        ←            ←       ←               ←                  ←
```

Each behavior wraps around the next one, creating a nested structure where outer behaviors see everything that inner behaviors do. This design enables powerful composition where behaviors can work together to provide comprehensive functionality.

## Behavior Registration and Execution Order

The order in which you register behaviors is crucial because it determines the sequence of execution. Think of this like getting dressed in the morning - you put on underwear before your shirt, and your shirt before your jacket. The order matters for the final result.

```csharp
builder.Services
    .AddFSMediator()
    .AddLoggingBehavior()           // 1st: Logs everything (outermost wrapper)
    .AddPerformanceBehavior()       // 2nd: Times everything inside logging
    .AddRetryBehavior()             // 3rd: Retries everything inside performance timing
    .AddCircuitBreakerBehavior()    // 4th: Protects the handler (innermost wrapper)
    .AddResourceManagementBehavior(); // 5th: Manages resources around handler execution
```

This creates a nested structure like Russian dolls:

```
┌─ Logging Behavior ──────────────────────────────────┐
│ ┌─ Performance Behavior ───────────────────────────┐ │
│ │ ┌─ Retry Behavior ─────────────────────────────┐ │ │
│ │ │ ┌─ Circuit Breaker Behavior ─────────────────┐ │ │ │
│ │ │ │ ┌─ Resource Management Behavior ─────────┐ │ │ │ │
│ │ │ │ │ Your Handler                         │ │ │ │ │
│ │ │ │ └─────────────────────────────────────────┘ │ │ │ │
│ │ │ └───────────────────────────────────────────────┘ │ │ │
│ │ └─────────────────────────────────────────────────────┘ │ │
│ └───────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

With this arrangement, the logging behavior sees every retry attempt, performance timing includes retry delays, and the circuit breaker directly protects your handler.

## Logging Behavior: Your Application's Flight Recorder

Logging behavior is like having a meticulous flight recorder for your application. It captures detailed information about every request, providing invaluable insights for debugging, monitoring, and optimization.

### Basic Logging Configuration

The simplest logging setup requires no configuration at all:

```csharp
builder.Services
    .AddFSMediator()
    .AddLoggingBehavior();  // Uses sensible defaults
```

This automatically logs:
- Request start and completion
- Execution duration for each request
- Exception details when requests fail
- Request type names for easy filtering

### Understanding Logging Output

Here's what you'll see in your logs with different scenarios:

```csharp
// Successful request
[12:34:56 INF] Handling request GetUserQuery
[12:34:56 INF] Request GetUserQuery handled successfully in 45ms

// Failed request
[12:34:58 INF] Handling request DeleteUserCommand  
[12:34:58 ERR] Request DeleteUserCommand failed after 123ms
System.InvalidOperationException: User 999 cannot be deleted because they have active orders
```

The logging behavior automatically extracts request type names and formats them in a consistent, searchable way. This makes it easy to filter logs by operation type when troubleshooting issues.

### Advanced Logging Scenarios

For more sophisticated logging needs, you can customize the behavior or create specialized logging for specific request types:

```csharp
// Custom logging for sensitive operations
public class SensitiveOperationLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<SensitiveOperationLoggingBehavior<TRequest, TResponse>> _logger;
    private readonly IAuditLogger _auditLogger;

    public async Task<TResponse> HandleAsync(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = GetUserIdFromRequest(request);
        
        // Enhanced logging for audit trails
        _auditLogger.LogSensitiveOperation(userId, requestName, request);
        
        try
        {
            var response = await next(cancellationToken);
            _auditLogger.LogSensitiveOperationSuccess(userId, requestName);
            return response;
        }
        catch (Exception ex)
        {
            _auditLogger.LogSensitiveOperationFailure(userId, requestName, ex);
            throw;
        }
    }
}
```

## Performance Behavior: Your Speed Monitor

Performance behavior acts like a speedometer for your application, continuously monitoring how long operations take and alerting you when things slow down. This early warning system helps you identify performance issues before they impact users.

### Basic Performance Monitoring

```csharp
builder.Services
    .AddFSMediator()
    .AddPerformanceBehavior();  // Default 500ms warning threshold
```

This configuration logs warnings whenever requests exceed 500 milliseconds:

```
[12:34:56 WRN] Long running request detected: GetUserReportQuery took 1,234ms
```

### Customizing Performance Thresholds

Different operations have different performance expectations. A simple user lookup should be fast, while generating a complex report might naturally take longer:

```csharp
builder.Services
    .AddFSMediator()
    .AddPerformanceBehavior(warningThresholdMs: 200);  // More sensitive threshold
```

For operations with varying performance characteristics, you might implement conditional thresholds:

```csharp
public class AdaptivePerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> HandleAsync(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        var threshold = GetThresholdForRequest(request);
        var stopwatch = Stopwatch.StartNew();
        
        var response = await next(cancellationToken);
        stopwatch.Stop();
        
        if (stopwatch.ElapsedMilliseconds > threshold)
        {
            _logger.LogWarning("Operation {RequestName} exceeded expected duration: {ActualMs}ms > {ThresholdMs}ms",
                typeof(TRequest).Name, stopwatch.ElapsedMilliseconds, threshold);
        }
        
        return response;
    }
    
    private int GetThresholdForRequest(TRequest request)
    {
        // Different thresholds for different operation types
        return request switch
        {
            IQuery => 100,          // Queries should be fast
            ICommand => 500,        // Commands can take longer
            IReportRequest => 5000, // Reports are naturally slower
            _ => 1000               // Default threshold
        };
    }
}
```

### Performance Metrics Integration

Performance behavior can integrate with metrics systems to provide dashboards and alerting:

```csharp
public class MetricsPerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IMetrics _metrics;

    public async Task<TResponse> HandleAsync(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var response = await next(cancellationToken);
            
            stopwatch.Stop();
            _metrics.RecordHistogram("request_duration", stopwatch.ElapsedMilliseconds, 
                new { operation = requestName, status = "success" });
            
            return response;
        }
        catch (Exception)
        {
            stopwatch.Stop();
            _metrics.RecordHistogram("request_duration", stopwatch.ElapsedMilliseconds, 
                new { operation = requestName, status = "error" });
            throw;
        }
    }
}
```

## Retry Behavior: The Persistent Problem Solver

Retry behavior is like having a persistent assistant who doesn't give up easily. When operations fail due to temporary issues, the retry behavior automatically attempts them again with intelligent timing strategies.

### Understanding Retry Presets

FS.Mediator provides several retry presets optimized for common scenarios. Understanding these presets helps you choose the right approach without needing to understand all the underlying complexity.

#### Conservative Preset: The Cautious Approach

```csharp
builder.Services
    .AddFSMediator()
    .AddRetryBehavior(RetryPreset.Conservative);
```

This preset is like a careful driver who doesn't take risks:
- **Max attempts**: 3 total (1 initial + 2 retries)
- **Delay strategy**: Fixed 500ms between attempts
- **Total timeout**: 10 seconds maximum
- **Best for**: User-facing operations where speed matters more than persistence

#### Aggressive Preset: The Determined Approach

```csharp
builder.Services
    .AddFSMediator()
    .AddRetryBehavior(RetryPreset.Aggressive);
```

This preset is like a determined salesperson who keeps trying:
- **Max attempts**: 6 total (1 initial + 5 retries)
- **Delay strategy**: Exponential backoff with jitter
- **Total timeout**: 2 minutes maximum
- **Best for**: Critical background operations that must succeed

#### Database Preset: The Database-Aware Approach

```csharp
builder.Services
    .AddFSMediator()
    .AddRetryBehavior(RetryPreset.Database);
```

This preset understands database-specific failure patterns:
- **Max attempts**: 4 total (1 initial + 3 retries)
- **Delay strategy**: Exponential backoff (1s, 2s, 4s)
- **Smart filtering**: Only retries database-related exceptions
- **Total timeout**: 30 seconds maximum
- **Best for**: Operations that interact with databases

### Custom Retry Configuration

When presets don't match your needs exactly, you can create custom retry configurations:

```csharp
builder.Services.AddRetryBehavior(options =>
{
    options.MaxRetryAttempts = 3;
    options.InitialDelay = TimeSpan.FromSeconds(1);
    options.Strategy = RetryStrategy.ExponentialBackoffWithJitter;
    options.MaxTotalRetryTime = TimeSpan.FromSeconds(30);
    
    // Custom logic for determining what to retry
    options.ShouldRetryPredicate = exception =>
    {
        // Retry network and database issues
        if (exception is HttpRequestException or TimeoutException)
            return true;
            
        // Don't retry business logic exceptions
        if (exception is ValidationException or BusinessRuleException)
            return false;
            
        // For unknown exceptions, be conservative
        return false;
    };
});
```

### Understanding Retry Strategies

Each retry strategy embodies a different philosophy for handling timing between attempts:

#### Fixed Delay Strategy

Fixed delay waits the same amount of time between each retry attempt:

```
Attempt 1: Immediate
Attempt 2: Wait 1 second
Attempt 3: Wait 1 second  
Attempt 4: Wait 1 second
```

This strategy works well when you know the typical recovery time for failures and want predictable timing.

#### Exponential Backoff Strategy

Exponential backoff doubles the wait time after each failure:

```
Attempt 1: Immediate
Attempt 2: Wait 1 second
Attempt 3: Wait 2 seconds
Attempt 4: Wait 4 seconds
Attempt 5: Wait 8 seconds
```

This strategy is excellent for overloaded systems because it gives them increasingly more time to recover.

#### Exponential Backoff with Jitter Strategy

This adds randomness to exponential backoff to prevent the "thundering herd" problem:

```
Attempt 1: Immediate
Attempt 2: Wait 1.2 seconds (1s ± 20% jitter)
Attempt 3: Wait 1.8 seconds (2s ± 20% jitter)
Attempt 4: Wait 4.3 seconds (4s ± 20% jitter)
```

Jitter ensures that multiple clients don't all retry at exactly the same time, which could overwhelm a recovering service.

## Circuit Breaker Behavior: The Protective Guardian

Circuit breaker behavior acts like a protective electrical breaker in your home. When it detects dangerous conditions (high failure rates), it "opens" to protect your system from further damage, then periodically tests whether conditions have improved.

### Understanding Circuit Breaker States

A circuit breaker operates in three distinct states, and understanding these states helps you configure and troubleshoot them effectively.

#### Closed State: Normal Operation

In the closed state, all requests pass through while the circuit breaker monitors failure rates:

```csharp
// Circuit breaker monitoring in closed state
var result = await _mediator.SendAsync(new GetUserQuery(123));  // ✅ Passes through normally
```

The circuit breaker silently tracks success and failure rates, building statistics to detect when problems begin.

#### Open State: Protective Mode

When failures exceed the threshold, the circuit breaker opens and immediately fails requests:

```csharp
// Circuit breaker in open state
try 
{
    var result = await _mediator.SendAsync(new GetUserQuery(123));  // ❌ Fails immediately
}
catch (CircuitBreakerOpenException ex)
{
    // Handle the circuit breaker being open
    _logger.LogWarning("Service temporarily unavailable: {RequestType}", ex.RequestType.Name);
    return new ErrorResult("Service temporarily unavailable, please try again later");
}
```

This might seem harsh, but it protects your system by preventing wasted resources on operations likely to fail.

#### Half-Open State: Testing Recovery

After the configured break duration, the circuit breaker enters half-open state to test recovery:

```csharp
// Circuit breaker testing recovery
var result = await _mediator.SendAsync(new GetUserQuery(123));  // ⚠️ Trial request
```

If trial requests succeed, the circuit breaker closes. If they fail, it opens again for another break period.

### Circuit Breaker Presets

Different scenarios require different circuit breaker configurations. FS.Mediator provides presets optimized for common situations.

#### Sensitive Preset: Hair-Trigger Protection

```csharp
builder.Services
    .AddFSMediator()
    .AddCircuitBreakerBehavior(CircuitBreakerPreset.Sensitive);
```

This preset trips quickly and is ideal for:
- Critical user-facing operations
- Services where even small failure rates are unacceptable
- Operations with strict SLA requirements

Configuration characteristics:
- **Failure threshold**: 30% (trips with relatively few failures)
- **Minimum throughput**: 3 requests (makes decisions quickly)
- **Sampling window**: 30 seconds (short observation period)
- **Break duration**: 15 seconds (quick recovery testing)

#### Balanced Preset: Reasonable Protection

```csharp
builder.Services
    .AddFSMediator()
    .AddCircuitBreakerBehavior(CircuitBreakerPreset.Balanced);
```

This preset provides reasonable protection for most scenarios:
- **Failure threshold**: 50% (tolerates moderate failure rates)
- **Minimum throughput**: 5 requests (needs more samples)
- **Sampling window**: 60 seconds (balanced observation period)
- **Break duration**: 30 seconds (moderate recovery time)

#### External API Preset: Forgiving Protection

```csharp
builder.Services
    .AddFSMediator()
    .AddCircuitBreakerBehavior(CircuitBreakerPreset.ExternalApi);
```

This preset is designed for protecting calls to external APIs:
- **Failure threshold**: 60% (tolerates higher failure rates)
- **Minimum throughput**: 8 requests (waits for statistical significance)
- **Sampling window**: 3 minutes (longer observation for network issues)
- **Break duration**: 60 seconds (gives external services time to recover)
- **Smart filtering**: Ignores client errors (4xx HTTP codes) when counting failures

### Advanced Circuit Breaker Configuration

For specialized scenarios, you can create custom circuit breaker configurations:

```csharp
builder.Services.AddCircuitBreakerBehavior(options =>
{
    options.FailureThresholdPercentage = 40.0;  // Open at 40% failure rate
    options.MinimumThroughput = 10;             // Need 10 requests for decision
    options.SamplingDuration = TimeSpan.FromMinutes(2);  // 2-minute observation window
    options.DurationOfBreak = TimeSpan.FromSeconds(45);  // 45-second break period
    options.TrialRequestCount = 3;              // Test recovery with 3 requests
    
    // Custom failure detection logic
    options.ShouldCountAsFailure = exception =>
    {
        // Don't count business logic exceptions as infrastructure failures
        if (exception is ValidationException or 
            BusinessRuleException or 
            UserNotFoundException)
        {
            return false;
        }
        
        // Count infrastructure and external service failures
        return exception is HttpRequestException or 
               TimeoutException or 
               SocketException or
               SqlException;
    };
});
```

This configuration distinguishes between business logic problems (which shouldn't trigger infrastructure protection) and genuine system failures.

## Resource Management Behavior: The Careful Steward

Resource management behavior acts like a careful household manager, monitoring resource usage and cleaning up before problems accumulate. This is especially important in long-running applications that process many requests over time.

### Understanding Memory Pressure

Memory pressure occurs when your application's memory usage grows faster than the garbage collector can reclaim it. Left unchecked, this leads to increasingly frequent garbage collections, poor performance, and eventually OutOfMemoryExceptions.

Resource management behavior monitors several key indicators:

```csharp
builder.Services.AddResourceManagementBehavior(options =>
{
    options.MaxMemoryThresholdBytes = 512_000_000;  // 512MB absolute limit
    options.MemoryGrowthRateThresholdBytesPerSecond = 10_000_000;  // 10MB/s growth rate
    options.MonitoringIntervalSeconds = 30;  // Check every 30 seconds
});
```

When thresholds are exceeded, the behavior can take corrective action based on the configured cleanup strategy.

### Resource Management Presets

Different deployment environments need different resource management approaches:

#### Memory Constrained Preset

```csharp
builder.Services
    .AddFSMediator()
    .AddResourceManagementBehavior(ResourceManagementPreset.MemoryConstrained);
```

Optimized for containers and memory-limited environments:
- **Memory threshold**: 256MB (conservative limit)
- **Growth rate threshold**: 5MB/s (early intervention)
- **Monitoring frequency**: Every 15 seconds (frequent checking)
- **Cleanup strategy**: Aggressive (maximum memory reclamation)
- **Auto garbage collection**: Enabled (proactive cleanup)

#### High Performance Preset

```csharp
builder.Services
    .AddFSMediator()
    .AddResourceManagementBehavior(ResourceManagementPreset.HighPerformance);
```

Optimized for performance-critical applications:
- **Memory threshold**: 1GB (generous limit)
- **Growth rate threshold**: 50MB/s (tolerates bursts)
- **Monitoring frequency**: Every 60 seconds (minimal overhead)
- **Cleanup strategy**: Conservative (minimal performance impact)
- **Auto garbage collection**: Disabled (let GC manage itself)

### Custom Resource Management Strategies

For specific scenarios, you can implement custom resource management:

```csharp
builder.Services.AddResourceManagementBehavior(options =>
{
    options.MaxMemoryThresholdBytes = 800_000_000;  // 800MB
    options.AutoTriggerGarbageCollection = true;
    options.CleanupStrategy = ResourceCleanupStrategy.Balanced;
    
    // Custom cleanup action
    options.CustomCleanupAction = context =>
    {
        // Clear application-specific caches
        _cacheManager.ClearExpiredEntries();
        
        // Close idle database connections
        _connectionPool.CloseIdleConnections();
        
        // Notify monitoring systems
        _metrics.RecordCounter("resource_cleanup_triggered", new 
        { 
            memory_usage = context.CurrentMemoryUsage,
            growth_rate = context.MemoryGrowthRate 
        });
    };
});
```

## Combining Behaviors for Maximum Effect

The real power of FS.Mediator behaviors comes from combining them intelligently. Different combinations serve different architectural patterns and operational requirements.

### Web Application Configuration

For typical web applications, this combination provides comprehensive coverage:

```csharp
builder.Services
    .AddFSMediator()
    .AddLoggingBehavior()                    // Observability for all operations
    .AddPerformanceBehavior(200)             // Web operations should be fast
    .AddRetryBehavior(RetryPreset.Database)  // Handle database hiccups
    .AddCircuitBreakerBehavior(CircuitBreakerPreset.Balanced)  // Reasonable protection
    .AddResourceManagementBehavior(ResourceManagementPreset.Balanced);  // Standard cleanup
```

### Microservice Configuration

Microservices need more aggressive protection against external failures:

```csharp
builder.Services
    .AddFSMediator()
    .AddLoggingBehavior()
    .AddPerformanceBehavior(500)             // Services can be slower than web
    .AddRetryBehavior(RetryPreset.HttpApi)   // Handle network issues
    .AddCircuitBreakerBehavior(CircuitBreakerPreset.ExternalApi)  // Protect against service failures
    .AddResourceManagementBehavior(ResourceManagementPreset.MemoryConstrained);  // Container-friendly
```

### Background Processing Configuration

Background processes need maximum resilience and resource efficiency:

```csharp
builder.Services
    .AddFSMediator()
    .AddLoggingBehavior()
    .AddPerformanceBehavior(5000)            // Background tasks can take longer
    .AddRetryBehavior(RetryPreset.Aggressive) // Must succeed eventually
    .AddCircuitBreakerBehavior(CircuitBreakerPreset.Resilient)  // Tolerate more failures
    .AddResourceManagementBehavior(ResourceManagementPreset.MemoryConstrained);  // Long-running efficiency
```

## Environment-Specific Configurations

Different environments need different behavior configurations to match their operational characteristics and monitoring capabilities.

### Development Environment

Development environments prioritize debugging and learning over operational concerns:

```csharp
if (builder.Environment.IsDevelopment())
{
    builder.Services
        .AddFSMediator()
        .AddLoggingBehavior()  // See everything that happens
        .AddPerformanceBehavior(100)  // Strict performance monitoring
        .AddResourceManagementBehavior(ResourceManagementPreset.Development);  // Detailed diagnostics
}
```

This configuration provides maximum visibility with relaxed operational constraints.

### Staging Environment

Staging environments should closely mirror production but with enhanced monitoring:

```csharp
if (builder.Environment.IsStaging())
{
    builder.Services
        .AddFSMediator()
        .AddLoggingBehavior()
        .AddPerformanceBehavior()
        .AddRetryBehavior(RetryPreset.Database)
        .AddCircuitBreakerBehavior(CircuitBreakerPreset.Sensitive)  // More sensitive for testing
        .AddResourceManagementBehavior(ResourceManagementPreset.Balanced);
}
```

### Production Environment

Production environments need the full suite of protections:

```csharp
if (builder.Environment.IsProduction())
{
    builder.Services
        .AddFSMediator()
        .AddLoggingBehavior()
        .AddPerformanceBehavior()
        .AddRetryBehavior(RetryPreset.Database)
        .AddCircuitBreakerBehavior(CircuitBreakerPreset.Balanced)
        .AddResourceManagementBehavior(ResourceManagementPreset.MemoryConstrained);
}
```

## Creating Custom Behaviors

When the built-in behaviors don't meet your specific needs, you can create custom behaviors that integrate seamlessly with the FS.Mediator pipeline.

### Simple Custom Behavior Example

Here's a behavior that adds correlation IDs to all requests:

```csharp
public class CorrelationIdBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<CorrelationIdBehavior<TRequest, TResponse>> _logger;

    public CorrelationIdBehavior(ILogger<CorrelationIdBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> HandleAsync(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        // Generate correlation ID if not present
        var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString();
        
        // Add to logging context
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RequestType"] = typeof(TRequest).Name
        });
        
        _logger.LogInformation("Processing request with correlation ID {CorrelationId}", correlationId);
        
        try
        {
            var response = await next(cancellationToken);
            _logger.LogInformation("Request completed successfully with correlation ID {CorrelationId}", correlationId);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Request failed with correlation ID {CorrelationId}", correlationId);
            throw;
        }
    }
}

// Register the custom behavior
builder.Services
    .AddFSMediator()
    .AddPipelineBehavior(typeof(CorrelationIdBehavior<,>));
```

### Advanced Custom Behavior with Configuration

Here's a more sophisticated behavior that implements rate limiting:

```csharp
public class RateLimitingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IRateLimitStore _rateLimitStore;
    private readonly RateLimitingOptions _options;
    private readonly ILogger<RateLimitingBehavior<TRequest, TResponse>> _logger;

    public async Task<TResponse> HandleAsync(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        var requestType = typeof(TRequest).Name;
        var clientId = GetClientIdentifier(request);
        var rateLimitKey = $"{clientId}:{requestType}";
        
        // Check rate limit
        var allowed = await _rateLimitStore.IsAllowedAsync(
            rateLimitKey, 
            _options.MaxRequests, 
            _options.TimeWindow);
        
        if (!allowed)
        {
            _logger.LogWarning("Rate limit exceeded for client {ClientId} on {RequestType}", 
                clientId, requestType);
            throw new RateLimitExceededException(requestType, clientId);
        }
        
        // Proceed with request
        return await next(cancellationToken);
    }
    
    private string GetClientIdentifier(TRequest request)
    {
        // Extract client identifier from request context
        // This could be user ID, IP address, API key, etc.
        return "default-client"; // Simplified for example
    }
}

// Configuration class
public class RateLimitingOptions
{
    public int MaxRequests { get; set; } = 100;
    public TimeSpan TimeWindow { get; set; } = TimeSpan.FromMinutes(1);
}

// Registration with configuration
builder.Services.AddSingleton(new RateLimitingOptions 
{ 
    MaxRequests = 50, 
    TimeWindow = TimeSpan.FromMinutes(1) 
});
builder.Services.AddPipelineBehavior(typeof(RateLimitingBehavior<,>));
```

## Behavior Performance Considerations

Understanding the performance impact of behaviors helps you make informed decisions about which ones to use and how to configure them.

### Measuring Behavior Overhead

Each behavior adds a small amount of overhead to request processing. Here are typical overhead measurements:

```
Baseline (no behaviors): ~0.5μs per request
+ Logging behavior: ~1.2μs additional
+ Performance behavior: ~0.8μs additional
+ Retry behavior (no retries): ~0.7μs additional
+ Circuit breaker (closed): ~0.5μs additional
+ Resource management: ~1.0μs additional
Total with all behaviors: ~4.7μs overhead
```

For most applications, this overhead is negligible compared to actual business logic execution time (typically milliseconds to seconds).

### Optimizing Behavior Performance

When performance is critical, consider these optimization strategies:

#### Conditional Behavior Registration

Register behaviors only for requests that need them:

```csharp
// Only add retry behavior for external service calls
builder.Services.AddConditionalPipelineBehavior<RetryBehavior<,>>(
    requestType => requestType.Namespace?.Contains("ExternalServices") == true);
```

#### Lightweight Behavior Implementations

For high-frequency operations, create optimized behavior implementations:

```csharp
public class FastLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> HandleAsync(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        // Minimal logging for high-frequency operations
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Processing {RequestType}", typeof(TRequest).Name);
        }
        
        return await next(cancellationToken);
    }
}
```

## Troubleshooting Behavior Issues

When behaviors don't work as expected, systematic troubleshooting helps identify and resolve issues quickly.

### Common Issues and Solutions

#### Behavior Not Executing

If a behavior doesn't seem to be running:

1. **Check registration order**: Behaviors execute in registration order
2. **Verify assembly scanning**: Ensure behaviors are in scanned assemblies
3. **Check generic constraints**: Ensure behavior applies to your request types

```csharp
// Debug behavior registration
public class DiagnosticBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> HandleAsync(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"DiagnosticBehavior executing for {typeof(TRequest).Name}");
        return await next(cancellationToken);
    }
}
```

#### Behavior Conflicts

When multiple behaviors interfere with each other:

1. **Review execution order**: Change registration order to resolve conflicts
2. **Check exception handling**: Ensure behaviors don't swallow exceptions inappropriately
3. **Verify resource sharing**: Ensure behaviors don't compete for shared resources

#### Performance Issues

When behaviors cause performance problems:

1. **Profile behavior overhead**: Measure individual behavior impact
2. **Optimize configuration**: Adjust thresholds and intervals
3. **Consider conditional registration**: Apply behaviors only where needed

## Next Steps in Behavior Configuration

Mastering behavior configuration opens up powerful possibilities for building robust, observable applications. Here's how to continue your journey:

### Immediate Next Steps

1. **[Interceptors Configuration](interceptors.md)** - Learn request/response transformation
2. **[Configuration Presets](presets.md)** - Understand all available presets
3. **[Custom Behaviors](custom-behaviors.md)** - Build your own specialized behaviors

### Advanced Topics

- **Behavior Composition Patterns**: Advanced ways to combine behaviors
- **Dynamic Behavior Registration**: Runtime behavior configuration
- **Behavior Testing Strategies**: How to test custom behaviors effectively
- **Performance Optimization**: Advanced techniques for high-performance scenarios

### Building Your Configuration Strategy

Remember that behavior configuration is an iterative process. Start with sensible defaults, monitor your application's behavior in different environments, and gradually refine your configuration based on real-world performance and failure patterns.

The goal is not to use every available behavior, but to choose the right combination that provides the protection and observability your application needs without unnecessary complexity or overhead. Think of it as building a tailored suit - it should fit your application's specific requirements perfectly.