# Resilience Patterns Overview

Welcome to one of the most crucial aspects of building production-ready applications with FS.Mediator. Resilience patterns are like having a skilled emergency response team for your software - they ensure your application can handle failures gracefully and continue operating even when things go wrong. Think of this as learning how to build software that's as robust as a well-designed bridge, capable of withstanding storms while keeping traffic flowing.

## Why Resilience Matters: The Reality of Distributed Systems

Let me start with a story that illustrates why resilience is absolutely critical in modern applications. Imagine you're running an e-commerce website during Black Friday. Everything is running smoothly until suddenly your payment service starts responding slowly. Without resilience patterns, here's what happens:

1. **The Cascade Begins**: Payment requests start timing out
2. **Resource Exhaustion**: Your application keeps trying to connect, using up all available connection pools
3. **System Overload**: Other parts of your system slow down because they can't get database connections
4. **Complete Failure**: Your entire website becomes unresponsive
5. **Business Impact**: You lose thousands of dollars per minute in sales

Now imagine the same scenario with proper resilience patterns in place:

1. **Early Detection**: Circuit breakers detect the payment service issues
2. **Intelligent Isolation**: The system stops sending requests to the failing service
3. **Graceful Degradation**: Customers can still browse and add items to cart, but payments are temporarily disabled
4. **Automatic Recovery**: When the payment service recovers, the circuit breaker automatically starts allowing requests again
5. **Business Continuity**: You maintain most functionality and customer confidence

This is the power of resilience patterns - they transform catastrophic failures into manageable service degradations.

## The Three Pillars of Resilience in FS.Mediator

FS.Mediator provides three fundamental resilience patterns that work together to create robust applications. Think of these as the three legs of a sturdy stool - each one provides essential support, and together they create a stable foundation.

### 1. Retry Patterns: The Persistent Problem Solver

Retry patterns are like having a determined friend who doesn't give up easily. When something fails, instead of immediately declaring defeat, the retry pattern says "Let me try that again" and attempts the operation a few more times with intelligent timing.

```csharp
// Configure retry behavior for database operations
builder.Services
    .AddFSMediator()
    .AddRetryBehavior(RetryPreset.Database);  // Intelligent retries for database issues
```

This simple configuration adds sophisticated retry logic to all your requests. When a database timeout occurs, the system automatically retries with exponential backoff, often resolving transient issues without any user impact.

### 2. Circuit Breaker: The Protective Guardian

Circuit breakers are like the electrical circuit breakers in your home - they protect your system by "opening" when they detect dangerous conditions, preventing damage to the entire system. When a service starts failing repeatedly, the circuit breaker stops sending requests to it, giving it time to recover.

```csharp
builder.Services
    .AddFSMediator()
    .AddCircuitBreakerBehavior(CircuitBreakerPreset.ExternalApi);  // Protect against failing APIs
```

The circuit breaker monitors failure rates and automatically transitions between three states: Closed (normal operation), Open (failing fast), and Half-Open (testing recovery).

### 3. Resource Management: The Careful Steward

Resource management patterns ensure your application doesn't consume unlimited memory or other system resources. Think of this as having a careful household manager who ensures you don't run out of essential supplies and cleans up waste before it becomes a problem.

```csharp
builder.Services
    .AddFSMediator()
    .AddResourceManagementBehavior(ResourceManagementPreset.MemoryConstrained);  // Careful memory management
```

This monitors memory usage, triggers garbage collection when needed, and prevents resource leaks from accumulating over time.

## Understanding Transient vs. Permanent Failures

One of the most important concepts in resilience engineering is understanding the difference between failures that are worth retrying and those that aren't. This is like a doctor knowing the difference between a temporary headache and a serious medical condition - the treatment approach is completely different.

### Transient Failures: The Temporary Setbacks

Transient failures are temporary problems that often resolve themselves if you wait a bit and try again. These are like traffic jams - frustrating but usually temporary.

Common examples include:
- Database connection timeouts during heavy load
- Network hiccups that cause API calls to fail
- Temporary service overload that resolves in seconds
- Resource contention that clears up quickly

```csharp
// Example of handling transient database failures
public class GetUserHandler : IRequestHandler<GetUserQuery, User>
{
    public async Task<User> HandleAsync(GetUserQuery request, CancellationToken cancellationToken)
    {
        // This might fail with a timeout during high load
        var user = await _repository.GetUserAsync(request.UserId);
        
        if (user == null)
            throw new UserNotFoundException($"User {request.UserId} not found");
            
        return user;
    }
}
```

With retry behavior enabled, database timeouts are automatically retried with intelligent backoff, often succeeding on the second or third attempt.

### Permanent Failures: The Fundamental Problems

Permanent failures are problems that won't be fixed by trying again. These are like trying to use a key that doesn't fit - you can try as many times as you want, but the fundamental problem remains.

Common examples include:
- Invalid input data (wrong email format)
- Business rule violations (insufficient account balance)
- Authentication failures (wrong password)
- Not found errors (user doesn't exist)

```csharp
// Example of permanent failure that shouldn't be retried
public class TransferMoneyHandler : IRequestHandler<TransferMoneyCommand, TransferResult>
{
    public async Task<TransferResult> HandleAsync(TransferMoneyCommand command, CancellationToken cancellationToken)
    {
        var account = await _repository.GetAccountAsync(command.FromAccountId);
        
        if (account.Balance < command.Amount)
        {
            // This is a business rule violation - retrying won't help
            throw new InsufficientFundsException($"Account {command.FromAccountId} has insufficient funds");
        }
        
        // Process the transfer...
    }
}
```

FS.Mediator's retry behavior is smart enough to distinguish between these types of failures and only retry appropriate exceptions.

## Configuring Intelligent Retry Strategies

FS.Mediator provides several retry strategies, each optimized for different scenarios. Understanding these strategies helps you choose the right approach for your specific needs.

### Fixed Delay: The Steady Approach

Fixed delay retries wait the same amount of time between each attempt. This is like knocking on a door every 5 seconds - predictable and steady.

```csharp
builder.Services.AddRetryBehavior(options =>
{
    options.Strategy = RetryStrategy.FixedDelay;
    options.InitialDelay = TimeSpan.FromSeconds(2);
    options.MaxRetryAttempts = 3;
});
```

Fixed delay works well when you know the typical recovery time for failures and want predictable timing.

### Exponential Backoff: The Escalating Approach

Exponential backoff increases the delay after each failure - first 1 second, then 2 seconds, then 4 seconds, and so on. This is like giving someone increasingly more time to answer when they're busy.

```csharp
builder.Services.AddRetryBehavior(options =>
{
    options.Strategy = RetryStrategy.ExponentialBackoff;
    options.InitialDelay = TimeSpan.FromSeconds(1);
    options.MaxRetryAttempts = 4;  // 1s, 2s, 4s, 8s delays
});
```

Exponential backoff is excellent for overloaded systems because it gives them increasingly more time to recover.

### Exponential Backoff with Jitter: The Considerate Approach

This adds randomness to exponential backoff to prevent the "thundering herd" problem - where many clients retry at exactly the same time, potentially overwhelming a recovering service.

```csharp
builder.Services.AddRetryBehavior(options =>
{
    options.Strategy = RetryStrategy.ExponentialBackoffWithJitter;
    options.InitialDelay = TimeSpan.FromSeconds(1);
    options.MaxRetryAttempts = 3;
});
```

Think of jitter as being polite in a crowded situation - instead of everyone rushing forward at exactly the same time, people stagger their attempts slightly.

## Circuit Breaker States and Transitions

Understanding how circuit breakers work internally helps you configure them effectively and debug issues when they occur. A circuit breaker is like a sophisticated electrical breaker that monitors the "electrical load" of your service calls.

### Closed State: Normal Operation

In the closed state, the circuit breaker allows all requests through while monitoring failure rates. This is like normal electrical operation in your home - everything works, but the breaker is watching for problems.

```csharp
// Circuit breaker in closed state
var result = await _mediator.SendAsync(new GetUserQuery(123));  // ✅ Request passes through normally
```

The circuit breaker tracks success and failure rates, building up statistics to detect when problems start occurring.

### Open State: Protective Mode

When failures exceed the configured threshold, the circuit breaker "opens" and immediately fails all requests without even attempting them. This is like an electrical breaker tripping to protect your house from electrical damage.

```csharp
// Circuit breaker in open state
try 
{
    var result = await _mediator.SendAsync(new GetUserQuery(123));  // ❌ Fails immediately
}
catch (CircuitBreakerOpenException)
{
    // Handle the circuit breaker being open
    return new UserResult { Error = "Service temporarily unavailable" };
}
```

This might seem harsh, but it's actually protective - it prevents your application from wasting resources on requests that are likely to fail anyway.

### Half-Open State: Testing Recovery

After the configured "break duration" expires, the circuit breaker enters half-open state and allows a limited number of trial requests through. This is like cautiously testing whether the electrical problem has been fixed.

```csharp
// Circuit breaker in half-open state - testing recovery
var result = await _mediator.SendAsync(new GetUserQuery(123));  // ⚠️ Trial request
```

If trial requests succeed, the circuit breaker closes and normal operation resumes. If they fail, it opens again for another break period.

## Practical Circuit Breaker Configuration

Different types of services require different circuit breaker configurations. Let's explore how to configure circuit breakers for common scenarios.

### External API Protection

External APIs are notorious for being unreliable, so they need protective circuit breakers:

```csharp
builder.Services.AddCircuitBreakerBehavior(options =>
{
    options.FailureThresholdPercentage = 60.0;  // Open when 60% of requests fail
    options.MinimumThroughput = 8;              // Need at least 8 requests to make a decision
    options.SamplingDuration = TimeSpan.FromMinutes(3);  // Look at 3 minutes of history
    options.DurationOfBreak = TimeSpan.FromSeconds(60);  // Stay open for 1 minute
    options.TrialRequestCount = 3;              // Test with 3 requests when half-open
});
```

This configuration tolerates more failures but responds quickly to widespread issues.

### Database Protection

Databases typically need more conservative protection because they're critical infrastructure:

```csharp
builder.Services.AddCircuitBreakerBehavior(options =>
{
    options.FailureThresholdPercentage = 40.0;  // Open when 40% of requests fail
    options.MinimumThroughput = 5;              // Need fewer requests for decision
    options.SamplingDuration = TimeSpan.FromMinutes(1);  // Shorter sampling window
    options.DurationOfBreak = TimeSpan.FromSeconds(45);  // Shorter break period
    options.TrialRequestCount = 2;              // Conservative trial testing
});
```

### Custom Failure Detection

You can customize what counts as a failure for different scenarios:

```csharp
builder.Services.AddCircuitBreakerBehavior(options =>
{
    options.ShouldCountAsFailure = exception =>
    {
        // Don't count business logic exceptions as circuit breaker failures
        if (exception is UserNotFoundException or ValidationException)
            return false;
            
        // Only count infrastructure failures
        return exception is TimeoutException or SocketException or HttpRequestException;
    };
});
```

This ensures that business logic problems don't trigger infrastructure protection mechanisms.

## Resource Management: Preventing Resource Exhaustion

Resource management in FS.Mediator goes beyond just memory - it's about ensuring your application can run indefinitely without accumulating problems. Think of this as having a good maintenance routine for your car - regular attention prevents major breakdowns.

### Memory Pressure Detection

FS.Mediator monitors memory usage patterns and takes action when pressure builds up:

```csharp
builder.Services.AddResourceManagementBehavior(options =>
{
    options.MaxMemoryThresholdBytes = 512_000_000;  // 512MB limit
    options.MemoryGrowthRateThresholdBytesPerSecond = 10_000_000;  // 10MB/s growth rate
    options.AutoTriggerGarbageCollection = true;
    options.CleanupStrategy = ResourceCleanupStrategy.Balanced;
});
```

When memory usage exceeds thresholds, the system can automatically trigger garbage collection and cleanup operations.

### Cleanup Strategies

Different cleanup strategies provide different balances between thoroughness and performance impact:

**Conservative Strategy**: Light cleanup with minimal performance impact
```csharp
options.CleanupStrategy = ResourceCleanupStrategy.Conservative;
// - Generation 0 garbage collection only
// - Clear weak references
// - Minimal disruption to ongoing operations
```

**Balanced Strategy**: Moderate cleanup with reasonable performance trade-offs
```csharp
options.CleanupStrategy = ResourceCleanupStrategy.Balanced;
// - Generation 0 and 1 garbage collection
// - Clear finalizer queue
// - Good balance of cleanup vs performance
```

**Aggressive Strategy**: Thorough cleanup prioritizing memory reclamation
```csharp
options.CleanupStrategy = ResourceCleanupStrategy.Aggressive;
// - Full garbage collection across all generations
// - Force disposal of tracked resources
// - Maximum cleanup at cost of temporary performance impact
```

## Combining Resilience Patterns for Maximum Effect

The real power of FS.Mediator's resilience features comes from combining multiple patterns. They work together like a coordinated defense system, each covering different types of failures.

### The Complete Resilience Stack

Here's how to configure a comprehensive resilience system:

```csharp
builder.Services
    .AddFSMediator()
    // Layer 1: Observability (see everything that happens)
    .AddLoggingBehavior()
    .AddPerformanceBehavior()
    
    // Layer 2: Resource Protection (prevent resource exhaustion)
    .AddResourceManagementBehavior(ResourceManagementPreset.Balanced)
    
    // Layer 3: Retry Logic (handle transient failures)
    .AddRetryBehavior(RetryPreset.Database)
    
    // Layer 4: Circuit Breaking (protect against cascade failures)
    .AddCircuitBreakerBehavior(CircuitBreakerPreset.Balanced);
```

This creates a defense system where each layer provides a different type of protection. Observability shows you what's happening, resource protection prevents exhaustion, retry logic handles temporary problems, and circuit breaking prevents cascade failures.

### Environment-Specific Configurations

Different environments need different resilience configurations:

```csharp
// Development environment - focused on debugging
if (builder.Environment.IsDevelopment())
{
    builder.Services
        .AddFSMediator()
        .AddLoggingBehavior()
        .AddPerformanceBehavior()
        .AddResourceManagementBehavior(ResourceManagementPreset.Development);
}

// Production environment - comprehensive protection
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

## Streaming-Specific Resilience Patterns

Streaming operations have unique resilience requirements because they can run for long periods and partially succeed. FS.Mediator provides specialized resilience patterns for streaming scenarios.

### Streaming Retry Challenges

When a stream fails after yielding 10,000 items, should you restart from the beginning or try to resume? FS.Mediator handles this intelligently:

```csharp
builder.Services
    .AddFSMediator()
    .AddStreamingRetryBehavior(options =>
    {
        options.MaxRetryAttempts = 2;
        options.InitialDelay = TimeSpan.FromSeconds(2);
        options.RetryStrategy = RetryStrategy.ExponentialBackoff;
        options.ResumeStrategy = StreamingRetryStrategy.RestartFromBeginning;  // Safe default
    });
```

The "restart from beginning" strategy is safer but potentially expensive. For advanced scenarios, you can implement resumption logic in your handlers.

### Streaming Circuit Protection

Streaming circuit breakers protect against long-running operations that might consume excessive resources:

```csharp
builder.Services.AddStreamingCircuitBreakerBehavior(options =>
{
    options.FailureThresholdPercentage = 60.0;  // Higher tolerance for streams
    options.MinimumThroughput = 3;              // Fewer samples needed
    options.SamplingDuration = TimeSpan.FromMinutes(5);  // Longer time window
    options.DurationOfBreak = TimeSpan.FromMinutes(2);   // Longer recovery time
});
```

### Streaming Resource Management

Long-running streams need continuous resource monitoring:

```csharp
builder.Services.AddStreamingResourceManagementBehavior(options =>
{
    options.MaxMemoryThresholdBytes = 256_000_000;  // Lower threshold for streams
    options.MonitoringIntervalSeconds = 30;         // Frequent monitoring
    options.AutoTriggerGarbageCollection = true;    // Proactive cleanup
});
```

This ensures that even streams processing millions of items maintain stable memory usage.

## Monitoring and Observability

Resilience patterns are only effective if you can see how they're performing. FS.Mediator provides comprehensive observability for all resilience features.

### Automatic Logging

All resilience patterns include detailed logging:

```
[INFO] Circuit breaker for GetUserQuery is half-open. Testing service recovery with trial request
[WARN] Retry attempt 2/3 for GetUserQuery failed with DatabaseTimeoutException: Connection timeout
[INFO] Request GetUserQuery succeeded after 2 retries in 3.2 seconds
[WARN] Resource pressure detected: Memory usage 580MB exceeds threshold 512MB
```

### Custom Monitoring Integration

You can integrate with your monitoring systems:

```csharp
builder.Services.AddRetryBehavior(options =>
{
    options.OnRetryAttempt = (attempt, exception, delay) =>
    {
        // Send metrics to your monitoring system
        _metrics.IncrementCounter("retry_attempts", new { operation = "database", attempt });
    };
});
```

### Health Check Integration

FS.Mediator integrates with ASP.NET Core health checks:

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<CircuitBreakerHealthCheck>("circuit-breakers")
    .AddCheck<ResourceUsageHealthCheck>("resource-usage");
```

## Common Resilience Anti-Patterns to Avoid

Learning resilience patterns also means understanding what not to do. Here are common mistakes and how to avoid them.

### Anti-Pattern 1: Retry All Exceptions

```csharp
// ❌ Wrong - retries everything including permanent failures
builder.Services.AddRetryBehavior(options =>
{
    options.ShouldRetryPredicate = _ => true;  // This will retry everything!
});
```

This wastes resources retrying things that will never succeed and can mask real application problems.

```csharp
// ✅ Correct - only retry transient failures
builder.Services.AddRetryBehavior(options =>
{
    options.ShouldRetryPredicate = ex => 
        ex is TimeoutException or SocketException or HttpRequestException;
});
```

### Anti-Pattern 2: Aggressive Circuit Breaker Settings

```csharp
// ❌ Wrong - too sensitive, will trip on minor issues
builder.Services.AddCircuitBreakerBehavior(options =>
{
    options.FailureThresholdPercentage = 10.0;  // Only 10% failures
    options.MinimumThroughput = 1;              // Based on single request
});
```

This creates a "hair trigger" circuit breaker that opens too easily and reduces system availability.

```csharp
// ✅ Correct - reasonable thresholds
builder.Services.AddCircuitBreakerBehavior(options =>
{
    options.FailureThresholdPercentage = 50.0;  // 50% failure rate
    options.MinimumThroughput = 5;              // Need several samples
});
```

### Anti-Pattern 3: Ignoring Resource Cleanup

```csharp
// ❌ Wrong - no resource management
public class DataProcessorHandler : IRequestHandler<ProcessDataCommand, Result>
{
    public async Task<Result> HandleAsync(ProcessDataCommand command, CancellationToken cancellationToken)
    {
        var largeData = await LoadLargeDataSet();  // Loads 500MB
        // Process data but never clean up
        return ProcessData(largeData);
    }
}
```

Without resource management, memory usage grows over time leading to eventual crashes.

```csharp
// ✅ Correct - proper resource management
builder.Services.AddResourceManagementBehavior(ResourceManagementPreset.MemoryConstrained);

public class DataProcessorHandler : IRequestHandler<ProcessDataCommand, Result>
{
    public async Task<Result> HandleAsync(ProcessDataCommand command, CancellationToken cancellationToken)
    {
        // Resource management behavior automatically monitors and cleans up
        var largeData = await LoadLargeDataSet();
        return ProcessData(largeData);
    }
}
```

## Building Resilient Microservices

In microservice architectures, resilience patterns become even more critical because failures can cascade across service boundaries. FS.Mediator helps you build resilient microservices by default.

### Service-to-Service Communication

When calling other microservices, layer multiple resilience patterns:

```csharp
// Configure resilience for external service calls
builder.Services
    .AddFSMediator()
    .AddRetryBehavior(RetryPreset.HttpApi)                    // Handle network issues
    .AddCircuitBreakerBehavior(CircuitBreakerPreset.ExternalApi)  // Protect against service failures
    .AddResourceManagementBehavior(ResourceManagementPreset.Balanced);  // Manage resources
```

### Graceful Degradation

Design your handlers to degrade gracefully when external services fail:

```csharp
public class GetEnrichedUserHandler : IRequestHandler<GetEnrichedUserQuery, EnrichedUser>
{
    public async Task<EnrichedUser> HandleAsync(GetEnrichedUserQuery query, CancellationToken cancellationToken)
    {
        // Get core user data (critical path)
        var user = await _mediator.SendAsync(new GetUserQuery(query.UserId));
        
        var enrichedUser = new EnrichedUser { User = user };
        
        try
        {
            // Try to get additional data (non-critical path)
            enrichedUser.Preferences = await _mediator.SendAsync(new GetUserPreferencesQuery(query.UserId));
        }
        catch (CircuitBreakerOpenException)
        {
            // Graceful degradation - continue without preferences
            enrichedUser.Preferences = UserPreferences.Default;
        }
        
        return enrichedUser;
    }
}
```

This approach ensures that essential functionality continues working even when optional services fail.

## Performance Impact of Resilience Patterns

Understanding the performance impact of resilience patterns helps you make informed trade-offs between robustness and speed.

### Baseline Performance

Each resilience pattern adds a small amount of overhead:

```
No behaviors: ~0.5μs per request
+ Logging: ~1.2μs per request  
+ Performance monitoring: ~1.5μs per request
+ Retry (no retries needed): ~2.0μs per request
+ Circuit breaker (closed): ~2.2μs per request
+ Resource management: ~2.5μs per request
```

For most applications, this overhead is negligible compared to actual business logic execution time.

### Retry Performance Impact

When retries are triggered, performance depends on the retry strategy:

```
Fixed delay (3 retries @ 1s each): ~3 seconds additional latency
Exponential backoff (3 retries): ~7 seconds additional latency (1s + 2s + 4s)
Jittered exponential: ~7 seconds ± 25% randomization
```

This is why proper retry configuration is crucial - you want enough resilience without excessive latency.

### Circuit Breaker Performance Characteristics

Circuit breakers actually improve performance during failures by failing fast:

```
Normal request with failing service: 30 seconds (timeout)
Circuit breaker open: <1ms (immediate failure)
```

The trade-off is temporary service unavailability in exchange for system stability and responsiveness.

## Testing Resilience Patterns

Testing resilience patterns requires simulating failure conditions and verifying the system responds appropriately. Here are effective strategies for testing resilience behavior.

### Unit Testing Retry Logic

```csharp
[Test]
public async Task Handler_Should_Retry_On_Transient_Failures()
{
    // Arrange
    var mockRepository = new Mock<IUserRepository>();
    mockRepository.SetupSequence(r => r.GetUserAsync(It.IsAny<int>()))
              .ThrowsAsync(new TimeoutException())  // First call fails
              .ThrowsAsync(new TimeoutException())  // Second call fails  
              .ReturnsAsync(new User { Id = 123 }); // Third call succeeds
    
    var handler = new GetUserHandler(mockRepository.Object);
    var query = new GetUserQuery(123);
    
    // Act
    var result = await handler.HandleAsync(query, CancellationToken.None);
    
    // Assert
    Assert.That(result.Id, Is.EqualTo(123));
    mockRepository.Verify(r => r.GetUserAsync(123), Times.Exactly(3));
}
```

### Integration Testing Circuit Breakers

```csharp
[Test]
public async Task CircuitBreaker_Should_Open_After_Repeated_Failures()
{
    // Arrange
    var services = new ServiceCollection()
        .AddFSMediator()
        .AddCircuitBreakerBehavior(options =>
        {
            options.FailureThresholdPercentage = 50.0;
            options.MinimumThroughput = 5;
        })
        .BuildServiceProvider();
    
    var mediator = services.GetRequiredService<IMediator>();
    
    // Act - cause multiple failures to trigger circuit breaker
    for (int i = 0; i < 6; i++)
    {
        try 
        {
            await mediator.SendAsync(new FailingQuery());
        }
        catch (Exception) 
        {
            // Expected failures
        }
    }
    
    // Assert - next call should fail fast due to circuit breaker
    var exception = await Assert.ThrowsAsync<CircuitBreakerOpenException>(
        () => mediator.SendAsync(new FailingQuery()));
    
    Assert.That(exception.RequestType, Is.EqualTo(typeof(FailingQuery)));
}
```

### Load Testing with Chaos Engineering

For production-level testing, introduce controlled failures and measure system behavior:

```csharp
// Chaos engineering test - randomly introduce failures
public class ChaosTestingService
{
    private readonly Random _random = new();
    
    public async Task<T> MaybeFailAsync<T>(Func<Task<T>> operation)
    {
        if (_random.NextDouble() < 0.1) // 10% failure rate
        {
            throw new TimeoutException("Simulated chaos failure");
        }
        
        return await operation();
    }
}
```

## Next Steps in Your Resilience Journey

Building resilient applications is an iterative process. Start with basic patterns and gradually add sophistication as your understanding and requirements grow.

### Immediate Next Steps

1. **[Retry Patterns Deep Dive](retry-patterns.md)** - Master intelligent retry strategies
2. **[Circuit Breaker Configuration](circuit-breaker.md)** - Protect your services effectively
3. **[Resource Management](resource-management.md)** - Prevent resource exhaustion
4. **[Backpressure Handling](backpressure.md)** - Manage overload conditions gracefully

### Advanced Topics

- **Bulkhead Pattern**: Isolate resources to prevent total failure
- **Timeout Management**: Set appropriate timeouts for different operations
- **Health Monitoring**: Proactive monitoring and alerting
- **Chaos Engineering**: Intentionally introduce failures to test resilience

### Building a Resilience Mindset

Remember that resilience is not just about adding behaviors to your code - it's about developing a mindset that expects and plans for failures. Every system will fail eventually; the question is whether you're prepared for it.

Think of resilience patterns as insurance for your applications. You hope you never need them, but when problems occur, you'll be grateful they're there. The small investment in configuration and testing pays enormous dividends when real failures happen.

Start simple with basic retry and circuit breaker patterns, then gradually add more sophisticated resilience features as your applications mature. Your users, your team, and your future self will thank you for building systems that gracefully handle the inevitable challenges of distributed computing.