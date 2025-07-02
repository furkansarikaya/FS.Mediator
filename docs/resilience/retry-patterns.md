# Retry Patterns Deep Dive

Welcome to the comprehensive guide on retry patterns in FS.Mediator! If resilience patterns are your application's emergency response system, then retry patterns are your first line of defense - the persistent problem solvers that turn temporary setbacks into eventual successes. Think of retry patterns as having a determined friend who doesn't give up easily; when something fails due to a temporary issue, they say "Let me try that again" and keep attempting with intelligent timing until they succeed or determine the problem is permanent.

## Understanding the Philosophy of Retries

Before diving into configuration details, it's crucial to understand the fundamental philosophy behind intelligent retries. Retry patterns are not about mindlessly repeating failed operations - they're about distinguishing between temporary problems that will resolve themselves and permanent problems that won't be fixed by trying again.

Consider this analogy: Imagine you're trying to call a friend, but the call fails. The retry pattern asks several intelligent questions:

1. **What type of failure was it?** Was the line busy (temporary) or was the number disconnected (permanent)?
2. **How many times have we tried?** Maybe the first failure was just bad timing
3. **How long should we wait?** Calling back immediately might hit the same busy signal
4. **How much total time should we spend?** We can't retry forever

This decision-making process is exactly what FS.Mediator's retry patterns implement for your application operations.

## The Anatomy of Intelligent Retries

Let's start by understanding what happens when you enable retry behavior and a request fails:

```csharp
// When you configure retry behavior
builder.Services
    .AddFSMediator()
    .AddRetryBehavior(RetryPreset.Database);

// And then send a request that might fail
var user = await _mediator.SendAsync(new GetUserQuery(123));
```

Here's the detailed flow when a database timeout occurs:

```
1. Request: GetUserQuery(123)
2. Handler: Calls database → TimeoutException
3. Retry Logic: "This looks like a transient database issue"
4. Wait: 1 second (initial delay)
5. Retry 1: Calls database → TimeoutException again
6. Wait: 2 seconds (exponential backoff)
7. Retry 2: Calls database → Success!
8. Return: User object to caller
```

The caller never knows that retries happened - they just get their result. But behind the scenes, the retry behavior transformed what would have been a failure into a success.

## Transient vs. Permanent Failures: The Critical Distinction

The most important concept in retry patterns is understanding which failures are worth retrying. This is where many applications go wrong - they either retry everything (wasting resources) or retry nothing (missing opportunities for resilience).

### Transient Failures: The Temporary Setbacks

Transient failures are temporary problems that often resolve themselves if you wait and try again. These are like traffic jams - frustrating but usually temporary.

**Classic Examples of Transient Failures:**

```csharp
// Database connection timeouts during high load
public class GetOrderHandler : IRequestHandler<GetOrderQuery, Order>
{
    public async Task<Order> HandleAsync(GetOrderQuery request, CancellationToken cancellationToken)
    {
        // This might fail with SqlException: "Timeout expired"
        // But succeeding on retry because database load decreased
        return await _repository.GetOrderAsync(request.OrderId);
    }
}

// Network hiccups causing API calls to fail
public class GetWeatherHandler : IRequestHandler<GetWeatherQuery, Weather>
{
    public async Task<Weather> HandleAsync(GetWeatherQuery request, CancellationToken cancellationToken)
    {
        // This might fail with HttpRequestException: "Connection timed out"
        // But succeed on retry because network issue resolved
        return await _weatherApiClient.GetWeatherAsync(request.City);
    }
}

// Temporary service overload
public class ProcessPaymentHandler : IRequestHandler<ProcessPaymentCommand, PaymentResult>
{
    public async Task<PaymentResult> HandleAsync(ProcessPaymentCommand request, CancellationToken cancellationToken)
    {
        // This might fail with "Service Unavailable" (503)
        // But succeed on retry because service recovered
        return await _paymentGateway.ProcessAsync(request.Payment);
    }
}
```

### Permanent Failures: The Fundamental Problems

Permanent failures are problems that won't be fixed by trying again. These are like trying to use a key that doesn't fit - you can try as many times as you want, but the fundamental problem remains.

**Examples of Permanent Failures:**

```csharp
// Business rule violations
public class TransferMoneyHandler : IRequestHandler<TransferMoneyCommand, TransferResult>
{
    public async Task<TransferResult> HandleAsync(TransferMoneyCommand request, CancellationToken cancellationToken)
    {
        var account = await _repository.GetAccountAsync(request.FromAccountId);
        
        if (account.Balance < request.Amount)
        {
            // This is a business rule violation - retrying won't help
            throw new InsufficientFundsException($"Account {request.FromAccountId} has insufficient funds");
        }
        
        return await ProcessTransfer(account, request);
    }
}

// Invalid input data
public class CreateUserHandler : IRequestHandler<CreateUserCommand, User>
{
    public async Task<User> HandleAsync(CreateUserCommand request, CancellationToken cancellationToken)
    {
        if (!IsValidEmail(request.Email))
        {
            // Invalid email format - retrying won't fix this
            throw new ValidationException($"Email '{request.Email}' is not valid");
        }
        
        return await CreateUser(request);
    }
}

// Authentication failures
public class GetSecureDataHandler : IRequestHandler<GetSecureDataQuery, SecureData>
{
    public async Task<SecureData> HandleAsync(GetSecureDataQuery request, CancellationToken cancellationToken)
    {
        if (!_authService.IsAuthorized(request.UserId, "SecureData"))
        {
            // Authorization failure - retrying won't change permissions
            throw new UnauthorizedException($"User {request.UserId} not authorized");
        }
        
        return await GetSecureData(request);
    }
}
```

## Configuring Smart Retry Predicates

The key to effective retry patterns is configuring intelligent predicates that distinguish between transient and permanent failures. FS.Mediator provides flexible configuration options for this critical decision-making logic.

### Default Retry Predicate

FS.Mediator includes a sensible default predicate that covers the most common transient failure scenarios:

```csharp
builder.Services.AddRetryBehavior(); // Uses intelligent defaults

// The default predicate handles these scenarios:
// ✅ HttpRequestException (network issues)
// ✅ SocketException (connection problems)
// ✅ TaskCanceledException (timeouts)
// ✅ Database timeouts (various provider-specific exceptions)
// ❌ ArgumentException (programming errors)
// ❌ ValidationException (business logic errors)
// ❌ SecurityException (authorization problems)
```

### Custom Retry Predicates for Specific Scenarios

For more precise control, you can create custom predicates tailored to your application's specific failure patterns:

```csharp
// Example 1: Database-focused retry predicate
builder.Services.AddRetryBehavior(options =>
{
    options.ShouldRetryPredicate = exception =>
    {
        // Retry database-related transient failures
        if (exception is SqlException sqlEx)
        {
            // Specific SQL error numbers that indicate transient issues
            return sqlEx.Number switch
            {
                -2 => true,      // Timeout
                2 => true,       // Connection timeout
                53 => true,      // Network path not found
                11001 => true,   // Host not found
                _ => false       // Other SQL errors are likely permanent
            };
        }
        
        // Retry connection and timeout issues
        return exception.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
               exception.Message.Contains("connection", StringComparison.OrdinalIgnoreCase);
    };
});

// Example 2: HTTP API retry predicate
builder.Services.AddRetryBehavior(options =>
{
    options.ShouldRetryPredicate = exception =>
    {
        // Retry HTTP-related transient failures
        if (exception is HttpRequestException httpEx)
        {
            // Check for specific HTTP status codes in the message
            var message = httpEx.Message.ToLowerInvariant();
            
            // Retry on server errors and some client errors
            return message.Contains("500") ||    // Internal Server Error
                   message.Contains("502") ||    // Bad Gateway
                   message.Contains("503") ||    // Service Unavailable
                   message.Contains("504") ||    // Gateway Timeout
                   message.Contains("408") ||    // Request Timeout
                   message.Contains("429");      // Too Many Requests
        }
        
        // Don't retry client errors (4xx) except those listed above
        return exception is SocketException or TaskCanceledException;
    };
});

// Example 3: Business-logic aware predicate
builder.Services.AddRetryBehavior(options =>
{
    options.ShouldRetryPredicate = exception =>
    {
        // Never retry business logic exceptions
        if (exception is ValidationException or 
            BusinessRuleException or 
            InsufficientFundsException or
            UserNotFoundException)
        {
            return false;
        }
        
        // Never retry security-related exceptions
        if (exception is UnauthorizedException or 
            SecurityException or 
            AuthenticationException)
        {
            return false;
        }
        
        // Only retry infrastructure and transient failures
        return exception is TimeoutException or 
               HttpRequestException or 
               SocketException or
               SqlException;
    };
});
```

## Retry Strategies: Timing Your Persistence

Once you've determined that a failure is worth retrying, the next question is: when should you retry? Different timing strategies work better for different types of failures and system characteristics.

### Fixed Delay Strategy: The Steady Approach

Fixed delay waits the same amount of time between each retry attempt. This is like knocking on a door every 5 seconds - predictable and steady.

```csharp
builder.Services.AddRetryBehavior(options =>
{
    options.Strategy = RetryStrategy.FixedDelay;
    options.InitialDelay = TimeSpan.FromSeconds(2);
    options.MaxRetryAttempts = 3;
});

// Timeline for a request that fails 3 times:
// Attempt 1: Immediate failure
// Wait 2 seconds
// Attempt 2: Failure
// Wait 2 seconds  
// Attempt 3: Failure
// Wait 2 seconds
// Attempt 4: Success (or final failure)
```

**When to use Fixed Delay:**
- When you know the typical recovery time for failures
- For systems with predictable failure patterns
- When you want consistent, predictable timing
- For testing and debugging scenarios

**Configuration Example for Fixed Delay:**

```csharp
// Configuration for a service that typically recovers within 3 seconds
builder.Services.AddRetryBehavior(options =>
{
    options.Strategy = RetryStrategy.FixedDelay;
    options.InitialDelay = TimeSpan.FromSeconds(3);
    options.MaxRetryAttempts = 2;  // Total of 3 attempts
    options.MaxTotalRetryTime = TimeSpan.FromSeconds(15);
});
```

### Exponential Backoff Strategy: The Escalating Approach

Exponential backoff increases the delay after each failure - first 1 second, then 2 seconds, then 4 seconds, and so on. This is like giving someone increasingly more time to answer when they're busy.

```csharp
builder.Services.AddRetryBehavior(options =>
{
    options.Strategy = RetryStrategy.ExponentialBackoff;
    options.InitialDelay = TimeSpan.FromSeconds(1);
    options.MaxRetryAttempts = 4;
});

// Timeline for a request that fails 4 times:
// Attempt 1: Immediate failure
// Wait 1 second
// Attempt 2: Failure  
// Wait 2 seconds
// Attempt 3: Failure
// Wait 4 seconds
// Attempt 4: Failure
// Wait 8 seconds
// Attempt 5: Success (or final failure)
```

**When to use Exponential Backoff:**
- For overloaded systems that need time to recover
- When failure cause might persist for varying durations
- For database connection issues during high load
- When calling rate-limited APIs

**Advanced Exponential Backoff Configuration:**

```csharp
// Configuration for database operations under load
builder.Services.AddRetryBehavior(options =>
{
    options.Strategy = RetryStrategy.ExponentialBackoff;
    options.InitialDelay = TimeSpan.FromMilliseconds(500);
    options.MaxRetryAttempts = 5;
    options.MaxTotalRetryTime = TimeSpan.FromSeconds(30);  // Safety timeout
    
    // Database-specific retry logic
    options.ShouldRetryPredicate = ex => 
        ex is SqlException sqlEx && (sqlEx.Number == -2 || sqlEx.Number == 2);
});
```

### Exponential Backoff with Jitter: The Considerate Approach

This adds randomness to exponential backoff to prevent the "thundering herd" problem where many clients retry at exactly the same time, potentially overwhelming a recovering service.

```csharp
builder.Services.AddRetryBehavior(options =>
{
    options.Strategy = RetryStrategy.ExponentialBackoffWithJitter;
    options.InitialDelay = TimeSpan.FromSeconds(1);
    options.MaxRetryAttempts = 3;
});

// Timeline for multiple clients (showing jitter effect):
// Client A: 1.2s, 1.8s, 4.3s delays
// Client B: 0.8s, 2.1s, 3.7s delays  
// Client C: 1.4s, 2.3s, 4.1s delays
// (All different due to jitter, spreading load over time)
```

**When to use Exponential Backoff with Jitter:**
- In high-concurrency scenarios with many clients
- When calling external services that might be overwhelmed
- For microservice-to-microservice communication
- When you want to be a "good citizen" in distributed systems

**Jitter Configuration for High-Concurrency Scenarios:**

```csharp
// Configuration for microservice communication
builder.Services.AddRetryBehavior(options =>
{
    options.Strategy = RetryStrategy.ExponentialBackoffWithJitter;
    options.InitialDelay = TimeSpan.FromMilliseconds(250);
    options.MaxRetryAttempts = 4;
    options.MaxTotalRetryTime = TimeSpan.FromSeconds(45);
    
    // Only retry transient HTTP failures
    options.ShouldRetryPredicate = ex => 
        ex is HttpRequestException or SocketException or TaskCanceledException;
});
```

## Preset Configurations: Battle-Tested Strategies

FS.Mediator provides several preset configurations that embody years of distributed systems experience. These presets are carefully tuned for common scenarios and can save you from having to understand all the nuances of retry theory.

### Conservative Preset: The Cautious Approach

The Conservative preset is like a careful driver who doesn't take risks. It retries fewer times with shorter delays, prioritizing speed over persistence.

```csharp
builder.Services.AddRetryBehavior(RetryPreset.Conservative);

// What this configures:
// - Max attempts: 3 total (1 initial + 2 retries)
// - Strategy: Fixed 500ms delay between attempts
// - Total timeout: 10 seconds maximum
// - Predicate: Standard transient failures only
```

**When to use Conservative:**
- User-facing web operations where speed matters
- Operations with strict latency requirements
- When you prefer to fail fast rather than persist
- Real-time or interactive scenarios

**Example scenarios for Conservative preset:**
```csharp
// Web API endpoint that needs to respond quickly
public class GetUserProfileHandler : IRequestHandler<GetUserProfileQuery, UserProfile>
{
    // Conservative retry is appropriate here because users are waiting
    public async Task<UserProfile> HandleAsync(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        return await _userService.GetProfileAsync(request.UserId);
    }
}
```

### Aggressive Preset: The Determined Approach

The Aggressive preset is like a determined salesperson who keeps trying. It uses more attempts with intelligent backoff to maximize the chances of eventual success.

```csharp
builder.Services.AddRetryBehavior(RetryPreset.Aggressive);

// What this configures:
// - Max attempts: 6 total (1 initial + 5 retries)
// - Strategy: Exponential backoff with jitter
// - Total timeout: 2 minutes maximum
// - Predicate: Comprehensive transient failure detection
```

**When to use Aggressive:**
- Critical background operations that must succeed
- Data processing jobs where persistence is more important than speed
- Operations with high business value
- Scenarios where retry cost is low compared to failure cost

**Example scenarios for Aggressive preset:**
```csharp
// Critical financial transaction that must succeed
public class ProcessPaymentHandler : IRequestHandler<ProcessPaymentCommand, PaymentResult>
{
    // Aggressive retry is appropriate for high-value operations
    public async Task<PaymentResult> HandleAsync(ProcessPaymentCommand request, CancellationToken cancellationToken)
    {
        return await _paymentGateway.ProcessPaymentAsync(request.Payment);
    }
}

// Important data synchronization operation
public class SyncCustomerDataHandler : IRequestHandler<SyncCustomerDataCommand, SyncResult>
{
    // Aggressive retry ensures data consistency
    public async Task<SyncResult> HandleAsync(SyncCustomerDataCommand request, CancellationToken cancellationToken)
    {
        return await _dataSync.SynchronizeAsync(request.CustomerId);
    }
}
```

### Database Preset: The Database-Aware Approach

The Database preset understands database-specific failure patterns and is tuned for common database scenarios.

```csharp
builder.Services.AddRetryBehavior(RetryPreset.Database);

// What this configures:
// - Max attempts: 4 total (1 initial + 3 retries)
// - Strategy: Exponential backoff (1s, 2s, 4s delays)
// - Total timeout: 30 seconds maximum
// - Predicate: Database-specific transient failures
//   - Connection timeouts
//   - Deadlock detection
//   - Temporary network issues
//   - But NOT business constraint violations
```

**Database-specific retry logic:**
```csharp
// The Database preset intelligently handles these scenarios:
public class DatabaseRetryExamples
{
    // ✅ WILL retry these database issues:
    public async Task<Order> GetOrder(int orderId)
    {
        // Retry on: "Timeout expired", "Connection timeout", "Deadlock victim"
        return await _context.Orders.FindAsync(orderId);
    }
    
    // ❌ WILL NOT retry these database issues:
    public async Task<User> CreateUser(string email)
    {
        // Won't retry on: Foreign key violations, unique constraint violations
        var user = new User { Email = email };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(); // May throw constraint violation
        return user;
    }
}
```

### HTTP API Preset: The Network-Aware Approach

The HTTP API preset is optimized for calling external APIs and web services, handling common network-related failure patterns.

```csharp
builder.Services.AddRetryBehavior(RetryPreset.HttpApi);

// What this configures:
// - Max attempts: 5 total (1 initial + 4 retries)
// - Strategy: Exponential backoff with jitter
// - Total timeout: 45 seconds maximum
// - Predicate: HTTP and network-specific failures
//   - 500, 502, 503, 504 server errors
//   - Network timeouts and connection issues
//   - But NOT 400, 401, 403, 404 client errors
```

**HTTP API retry scenarios:**
```csharp
// The HttpApi preset handles these scenarios intelligently:
public class ApiRetryExamples
{
    // ✅ WILL retry these HTTP scenarios:
    public async Task<WeatherData> GetWeather(string city)
    {
        // Retries on: 500 Internal Server Error, 503 Service Unavailable,
        // network timeouts, connection refused
        return await _httpClient.GetFromJsonAsync<WeatherData>($"/weather/{city}");
    }
    
    // ❌ WILL NOT retry these HTTP scenarios:
    public async Task<UserData> GetUser(int userId)
    {
        // Won't retry on: 404 Not Found, 401 Unauthorized, 400 Bad Request
        return await _httpClient.GetFromJsonAsync<UserData>($"/users/{userId}");
    }
}
```

## Advanced Retry Configuration Patterns

Beyond the presets, FS.Mediator supports sophisticated retry configurations for complex scenarios.

### Request-Type Specific Retry Configuration

Different types of operations might need different retry strategies:

```csharp
// Global default retry configuration
builder.Services.AddRetryBehavior(RetryPreset.Conservative);

// Override for specific request types
builder.Services.Configure<RetryPolicyOptions<CriticalPaymentCommand>>(options =>
{
    options.MaxRetryAttempts = 5;
    options.Strategy = RetryStrategy.ExponentialBackoffWithJitter;
    options.MaxTotalRetryTime = TimeSpan.FromMinutes(2);
});

builder.Services.Configure<RetryPolicyOptions<QuickLookupQuery>>(options =>
{
    options.MaxRetryAttempts = 1;  // Only retry once for quick lookups
    options.Strategy = RetryStrategy.FixedDelay;
    options.InitialDelay = TimeSpan.FromMilliseconds(100);
});
```

### Custom Retry Behavior with Business Logic

For complex scenarios, you can create custom retry behaviors that incorporate business logic:

```csharp
public class BusinessAwareRetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly RetryPolicyOptions _options;
    private readonly IBusinessRuleEngine _businessRules;

    public async Task<TResponse> HandleAsync(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        var attempts = 0;
        var maxAttempts = GetMaxAttemptsForRequest(request);
        var startTime = DateTime.UtcNow;

        while (attempts <= maxAttempts)
        {
            try
            {
                return await next(cancellationToken);
            }
            catch (Exception ex)
            {
                attempts++;
                
                // Business-aware retry decision
                if (!ShouldRetryBasedOnBusinessContext(request, ex, attempts))
                {
                    throw;
                }
                
                // Check total time constraint
                if (DateTime.UtcNow - startTime > _options.MaxTotalRetryTime)
                {
                    throw;
                }
                
                // Calculate delay based on request priority
                var delay = CalculateDelayForRequest(request, attempts);
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new InvalidOperationException("Retry loop completed without result");
    }

    private bool ShouldRetryBasedOnBusinessContext<T>(T request, Exception exception, int attemptNumber)
    {
        // Incorporate business rules into retry decisions
        if (_businessRules.IsHighPriorityOperation(request))
        {
            return attemptNumber <= 5; // More retries for high priority
        }
        
        if (_businessRules.IsOffHours())
        {
            return attemptNumber <= 3; // Fewer retries during off hours
        }
        
        return attemptNumber <= 2; // Standard retry count
    }

    private TimeSpan CalculateDelayForRequest<T>(T request, int attemptNumber)
    {
        var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, attemptNumber));
        
        // VIP customers get shorter delays
        if (_businessRules.IsVipCustomerOperation(request))
        {
            return TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * 0.5);
        }
        
        return baseDelay;
    }
}
```

### Environment-Specific Retry Configuration

Different environments often need different retry strategies:

```csharp
// Development environment - fail fast for debugging
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddRetryBehavior(options =>
    {
        options.MaxRetryAttempts = 1;  // Minimal retries in dev
        options.InitialDelay = TimeSpan.FromMilliseconds(100);
        options.Strategy = RetryStrategy.FixedDelay;
    });
}

// Staging environment - moderate retries for testing
else if (builder.Environment.IsStaging())
{
    builder.Services.AddRetryBehavior(RetryPreset.Conservative);
}

// Production environment - comprehensive retries for resilience
else if (builder.Environment.IsProduction())
{
    builder.Services.AddRetryBehavior(RetryPreset.Database);
}
```

## Monitoring and Observability for Retries

Understanding how your retry patterns are performing in production is crucial for optimization and troubleshooting.

### Built-in Retry Logging

FS.Mediator automatically logs retry attempts with detailed information:

```csharp
// When you enable logging behavior along with retry behavior
builder.Services
    .AddFSMediator()
    .AddLoggingBehavior()      // Essential for seeing retry activity
    .AddRetryBehavior(RetryPreset.Database);

// You'll see logs like this:
// [INFO] Handling request GetUserQuery
// [WARN] Request GetUserQuery failed on attempt 1 with SqlException: Timeout expired. Retrying in 1000ms
// [WARN] Request GetUserQuery failed on attempt 2 with SqlException: Timeout expired. Retrying in 2000ms  
// [INFO] Request GetUserQuery succeeded after 2 retries in 3.2 seconds
```

### Custom Retry Metrics

For production monitoring, you can capture retry metrics:

```csharp
public class MetricsRetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IMetrics _metrics;
    private readonly RetryPolicyOptions _options;

    public async Task<TResponse> HandleAsync(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var totalAttempts = 0;
        var startTime = DateTime.UtcNow;

        for (var attempt = 0; attempt <= _options.MaxRetryAttempts; attempt++)
        {
            totalAttempts++;
            
            try
            {
                var result = await next(cancellationToken);
                
                // Record success metrics
                _metrics.RecordCounter("retry_attempts_total", new 
                { 
                    operation = requestName, 
                    outcome = "success",
                    attempts = totalAttempts 
                });
                
                if (totalAttempts > 1)
                {
                    var totalTime = DateTime.UtcNow - startTime;
                    _metrics.RecordHistogram("retry_success_duration", totalTime.TotalMilliseconds, new
                    {
                        operation = requestName,
                        attempts = totalAttempts
                    });
                }
                
                return result;
            }
            catch (Exception ex)
            {
                if (attempt >= _options.MaxRetryAttempts || !_options.ShouldRetryPredicate(ex))
                {
                    // Record failure metrics
                    _metrics.RecordCounter("retry_attempts_total", new 
                    { 
                        operation = requestName, 
                        outcome = "failure",
                        attempts = totalAttempts,
                        exception_type = ex.GetType().Name
                    });
                    
                    throw;
                }
                
                // Calculate delay and continue
                var delay = CalculateDelay(attempt);
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new InvalidOperationException("Retry loop completed without result");
    }
}
```

### Retry Health Checks

Monitor the health of your retry patterns:

```csharp
public class RetryHealthCheck : IHealthCheck
{
    private readonly IRetryMetricsStore _metricsStore;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        var recentMetrics = await _metricsStore.GetRecentRetryMetricsAsync(TimeSpan.FromMinutes(5));
        
        var totalOperations = recentMetrics.TotalOperations;
        var operationsRequiringRetries = recentMetrics.OperationsWithRetries;
        var retryRate = totalOperations > 0 ? (double)operationsRequiringRetries / totalOperations : 0;
        
        var data = new Dictionary<string, object>
        {
            ["total_operations"] = totalOperations,
            ["operations_with_retries"] = operationsRequiringRetries,
            ["retry_rate"] = retryRate,
            ["average_retry_count"] = recentMetrics.AverageRetryCount
        };
        
        if (retryRate > 0.3) // More than 30% of operations need retries
        {
            return HealthCheckResult.Degraded("High retry rate detected", null, data);
        }
        
        if (retryRate > 0.5) // More than 50% of operations need retries
        {
            return HealthCheckResult.Unhealthy("Excessive retry rate detected", null, data);
        }
        
        return HealthCheckResult.Healthy("Retry patterns functioning normally", data);
    }
}
```

## Common Retry Anti-Patterns and How to Avoid Them

Learning what NOT to do is just as important as learning best practices. Here are common retry anti-patterns and their solutions.

### Anti-Pattern 1: Retrying Everything

```csharp
// ❌ Wrong - retries everything including permanent failures
builder.Services.AddRetryBehavior(options =>
{
    options.ShouldRetryPredicate = _ => true;  // This will retry everything!
    options.MaxRetryAttempts = 5;
});

// This will waste resources retrying things like:
// - ValidationException (bad input data)
// - UnauthorizedException (permission issues)  
// - ArgumentNullException (programming errors)
```

**Solution**: Use intelligent retry predicates that only retry transient failures:

```csharp
// ✅ Correct - only retry transient failures
builder.Services.AddRetryBehavior(options =>
{
    options.ShouldRetryPredicate = ex => 
        ex is TimeoutException or 
        SocketException or 
        HttpRequestException or
        SqlException sqlEx && IsTransientSqlError(sqlEx);
});
```

### Anti-Pattern 2: No Maximum Total Time

```csharp
// ❌ Wrong - could retry forever in extreme cases
builder.Services.AddRetryBehavior(options =>
{
    options.MaxRetryAttempts = 10;  // 10 retries
    options.Strategy = RetryStrategy.ExponentialBackoff;
    options.InitialDelay = TimeSpan.FromSeconds(1);
    // Missing: MaxTotalRetryTime constraint
});

// With exponential backoff, this could take over 17 minutes!
// (1 + 2 + 4 + 8 + 16 + 32 + 64 + 128 + 256 + 512 = 1023 seconds)
```

**Solution**: Always set reasonable total time limits:

```csharp
// ✅ Correct - bounded total retry time
builder.Services.AddRetryBehavior(options =>
{
    options.MaxRetryAttempts = 5;
    options.Strategy = RetryStrategy.ExponentialBackoff;
    options.InitialDelay = TimeSpan.FromSeconds(1);
    options.MaxTotalRetryTime = TimeSpan.FromSeconds(30);  // Safety timeout
});
```

### Anti-Pattern 3: Ignoring Cancellation

```csharp
// ❌ Wrong - doesn't respect cancellation tokens
public class BadRetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt <= 3; attempt++)
        {
            try
            {
                return await next(CancellationToken.None); // Wrong - ignores cancellation
            }
            catch (Exception)
            {
                await Task.Delay(1000); // Wrong - doesn't pass cancellation token
            }
        }
        throw new Exception("All retries failed");
    }
}
```

**Solution**: Always respect cancellation tokens:

```csharp
// ✅ Correct - proper cancellation handling
public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
{
    for (int attempt = 0; attempt <= 3; attempt++)
    {
        try
        {
            return await next(cancellationToken); // Correct - passes cancellation
        }
        catch (Exception ex) when (ShouldRetry(ex, attempt))
        {
            await Task.Delay(1000, cancellationToken); // Correct - cancellable delay
        }
    }
    throw new Exception("All retries failed");
}
```

## Testing Retry Patterns

Testing retry behavior requires simulating failure conditions and verifying the retry logic works correctly.

### Unit Testing Retry Logic

```csharp
[Test]
public async Task RetryBehavior_Should_Retry_Transient_Failures()
{
    // Arrange
    var mockHandler = new Mock<IRequestHandler<TestQuery, string>>();
    var query = new TestQuery();
    
    // Setup: fail twice, then succeed
    mockHandler.SetupSequence(h => h.HandleAsync(query, It.IsAny<CancellationToken>()))
              .ThrowsAsync(new TimeoutException("Database timeout"))
              .ThrowsAsync(new TimeoutException("Database timeout"))  
              .ReturnsAsync("Success");
    
    var retryBehavior = new RetryBehavior<TestQuery, string>(
        Mock.Of<ILogger<RetryBehavior<TestQuery, string>>>(),
        new RetryPolicyOptions 
        { 
            MaxRetryAttempts = 3,
            InitialDelay = TimeSpan.FromMilliseconds(10) // Fast for testing
        });
    
    // Act
    var result = await retryBehavior.HandleAsync(query, 
        ct => mockHandler.Object.HandleAsync(query, ct), 
        CancellationToken.None);
    
    // Assert
    Assert.That(result, Is.EqualTo("Success"));
    mockHandler.Verify(h => h.HandleAsync(query, It.IsAny<CancellationToken>()), Times.Exactly(3));
}

[Test]
public async Task RetryBehavior_Should_Not_Retry_Permanent_Failures()
{
    // Arrange
    var mockHandler = new Mock<IRequestHandler<TestQuery, string>>();
    var query = new TestQuery();
    
    mockHandler.Setup(h => h.HandleAsync(query, It.IsAny<CancellationToken>()))
              .ThrowsAsync(new ValidationException("Invalid input"));
    
    var retryBehavior = new RetryBehavior<TestQuery, string>(
        Mock.Of<ILogger<RetryBehavior<TestQuery, string>>>(),
        new RetryPolicyOptions());
    
    // Act & Assert
    var exception = await Assert.ThrowsAsync<ValidationException>(
        () => retryBehavior.HandleAsync(query, 
            ct => mockHandler.Object.HandleAsync(query, ct), 
            CancellationToken.None));
    
    // Should only call once - no retries for permanent failures
    mockHandler.Verify(h => h.HandleAsync(query, It.IsAny<CancellationToken>()), Times.Once);
}
```

### Integration Testing with Simulated Failures

```csharp
[Test]
public async Task EndToEnd_Retry_Integration_Test()
{
    // Arrange
    var services = new ServiceCollection()
        .AddFSMediator()
        .AddRetryBehavior(options =>
        {
            options.MaxRetryAttempts = 2;
            options.InitialDelay = TimeSpan.FromMilliseconds(50);
        })
        .AddScoped<IUnreliableService, SimulatedUnreliableService>()
        .BuildServiceProvider();
    
    var mediator = services.GetRequiredService<IMediator>();
    
    // Act
    var result = await mediator.SendAsync(new UnreliableQuery("test"));
    
    // Assert
    Assert.That(result, Is.Not.Null);
    
    // Verify the unreliable service was called multiple times
    var service = services.GetRequiredService<IUnreliableService>() as SimulatedUnreliableService;
    Assert.That(service.CallCount, Is.EqualTo(3)); // 1 initial + 2 retries
}

public class SimulatedUnreliableService : IUnreliableService
{
    public int CallCount { get; private set; }
    
    public async Task<string> ProcessAsync(string input)
    {
        CallCount++;
        
        // Fail first two calls, succeed on third
        if (CallCount <= 2)
        {
            throw new TimeoutException($"Simulated timeout on call {CallCount}");
        }
        
        return $"Processed: {input}";
    }
}
```

## Next Steps

Now that you've mastered retry patterns, explore these related topics:

- **[Circuit Breaker Patterns](circuit-breaker.md)** - Protect against cascade failures
- **[Backpressure Handling](backpressure.md)** - Manage overload conditions
- **[Resource Management](resource-management.md)** - Prevent resource exhaustion
- **[Streaming Retry Patterns](../streaming/advanced-streaming.md)** - Retry strategies for streaming operations

Remember, retry patterns are just one layer of your application's resilience strategy. They work best when combined with other patterns like circuit breakers and proper monitoring. The goal is not to retry everything, but to retry the right things at the right times with the right intervals.

Start with sensible presets, monitor how they perform in your specific environment, and gradually refine your retry configuration based on real-world failure patterns. Your application's resilience will improve dramatically with thoughtfully configured retry patterns.