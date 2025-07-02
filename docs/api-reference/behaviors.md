# API Reference: Behaviors

## What Are Behaviors?

Behaviors are middleware components that wrap around your request handlers, allowing you to add cross-cutting concerns like:
- Logging
- Performance monitoring
- Error handling
- Validation
- Retry logic

Think of them like layers around your core business logic that handle technical concerns consistently.

## How Behaviors Work

1. A request comes in
2. Each behavior executes **before** your handler
3. Your handler processes the request
4. Each behavior executes **after** your handler (in reverse order)
5. The response is returned

## Core Behavior Interface

All behaviors implement this interface:

```csharp
public interface IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    Task<TResponse> HandleAsync(
        TRequest request,                  // The incoming request
        RequestHandlerDelegate<TResponse> next,  // Function to call the next behavior/handler
        CancellationToken cancellationToken);
}
```

## Built-in Behaviors Explained

### 1. Logging Behavior

**Purpose**: Automatically logs request start/end and exceptions

**Basic Usage**:
```csharp
builder.Services.AddLoggingBehavior();
```

**What You Get**:
```
[INFO] Handling request GetUserQuery
[INFO] Request GetUserQuery completed in 45ms
[ERROR] Request DeleteUser failed: User not found
```

### 2. Performance Behavior

**Purpose**: Tracks and warns about slow requests

**Basic Usage**:
```csharp
// Warn if requests take longer than 500ms
builder.Services.AddPerformanceBehavior(warningThresholdMs: 500); 
```

**What You Get**:
```
[WARN] Slow request detected: GenerateReport took 1200ms
```

### 3. Retry Behavior

**Purpose**: Automatically retries failed requests

**Basic Usage**:
```csharp
// Retry 3 times with 1 second delay
builder.Services.AddRetryBehavior(options => 
{
    options.MaxRetryAttempts = 3;
    options.InitialDelay = TimeSpan.FromSeconds(1);
});
```

## Complete Beginner Example

Here's how a new user might set up basic behaviors:

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// 1. Add mediator with behaviors
builder.Services
    .AddFSMediator()
    .AddLoggingBehavior()       // First: Log all requests
    .AddPerformanceBehavior()   // Second: Track performance
    .AddRetryBehavior();        // Third: Handle transient failures

var app = builder.Build();
app.Run();
```

## Creating Your First Custom Behavior

Let's make a simple behavior that measures execution time:

```csharp
public class TimingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly ILogger<TimingBehavior<TRequest, TResponse>> _logger;

    public TimingBehavior(ILogger<TimingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Before handler
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Call next behavior/handler
            var response = await next();
            
            // After successful handler
            _logger.LogInformation("Request {RequestType} took {ElapsedMs}ms",
                typeof(TRequest).Name, 
                stopwatch.ElapsedMilliseconds);
            
            return response;
        }
        catch (Exception ex)
        {
            // After failed handler
            _logger.LogError(ex, "Request {RequestType} failed after {ElapsedMs}ms",
                typeof(TRequest).Name,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
```

**Register your custom behavior**:
```csharp
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TimingBehavior<,>));
```

## Behavior Execution Order Matters!

Behaviors execute in registration order. This is important:

```csharp
builder.Services
    .AddFSMediator()
    .AddLoggingBehavior()    // 1. Logs first
    .AddRetryBehavior()      // 2. Then retries (will retry logged failures)
    .AddTimingBehavior();    // 3. Times the whole process
```

## Next Steps for Beginners

1. Try adding basic behaviors to a test project
2. Create a simple custom behavior
3. Experiment with different execution orders
4. Check out the [configuration guide](../configuration/behaviors.md)
5. Learn about [built-in resilience patterns](../resilience/overview.md)