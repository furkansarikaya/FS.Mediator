# FS.Mediator

[![NuGet Version](https://img.shields.io/nuget/v/FS.Mediator.svg)](https://www.nuget.org/packages/FS.Mediator/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/FS.Mediator.svg)](https://www.nuget.org/packages/FS.Mediator/)
[![GitHub License](https://img.shields.io/github/license/furkansarikaya/FS.Mediator)](https://github.com/furkansarikaya/FS.Mediator/blob/main/LICENSE)
[![GitHub Stars](https://img.shields.io/github/stars/furkansarikaya/FS.Mediator.svg)](https://github.com/furkansarikaya/FS.Mediator/stargazers)

**A comprehensive, high-performance mediator library for .NET with advanced streaming capabilities and enterprise-grade resilience patterns.**

FS.Mediator isn't just another mediator implementation. It's a complete solution for building scalable, resilient applications with sophisticated data processing capabilities. Whether you're building microservices, processing large datasets, or creating real-time applications, FS.Mediator provides the tools you need.

## ✨ Why FS.Mediator?

Imagine you're building a modern application that needs to handle thousands of requests per second, process large data streams, and remain resilient under pressure. Traditional mediator libraries give you the basic request/response pattern, but when you need enterprise-grade features like circuit breakers, backpressure handling, and streaming operations, you're on your own.

FS.Mediator bridges this gap by providing **everything you need in one cohesive package**:

- 🎯 **Clean Architecture**: Decoupled request/response and notification patterns
- 🌊 **Advanced Streaming**: Process millions of records without memory issues
- 🛡️ **Built-in Resilience**: Circuit breakers, retry policies, and error handling
- ⚡ **Performance Optimized**: Backpressure handling and resource management
- 📊 **Health Monitoring**: Real-time diagnostics and performance tracking
- 🔧 **Highly Configurable**: Multiple presets for common scenarios

## 🚀 Quick Start

### Installation

```bash
dotnet add package FS.Mediator
```

### Basic Setup

```csharp
// Program.cs
using FS.Mediator.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add FS.Mediator with automatic handler discovery
builder.Services.AddFSMediator();

var app = builder.Build();
```

### Your First Request

```csharp
// Define a request
public record GetUserQuery(int Id) : IRequest<User>;

// Create a handler
public class GetUserHandler : IRequestHandler<GetUserQuery, User>
{
    public async Task<User> HandleAsync(GetUserQuery request, CancellationToken cancellationToken)
    {
        // Your business logic here
        return await _userRepository.GetByIdAsync(request.Id);
    }
}

// Use in your controller
[ApiController]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{id}")]
    public async Task<User> GetUser(int id)
    {
        return await _mediator.SendAsync(new GetUserQuery(id));
    }
}
```

That's it! You now have a clean, decoupled architecture with powerful features ready to use.

## 🌟 Key Features

### 1. Traditional Mediator Pattern

Handle requests, responses, and notifications with clean separation of concerns:

```csharp
// Request with response
public record CreateOrderCommand(string CustomerName, decimal Amount) : IRequest<Order>;

// Request without response
public record LogUserActivityCommand(int UserId, string Action) : IRequest<Unit>;

// Notification (multiple handlers)
public record OrderCreatedNotification(Order Order) : INotification;
```

### 2. Advanced Streaming Operations

Process large datasets efficiently without loading everything into memory:

```csharp
// Streaming request
public record GetAllUsersStreamQuery(string Department) : IStreamRequest<User>;

// Streaming handler
public class GetAllUsersStreamHandler : IStreamRequestHandler<GetAllUsersStreamQuery, User>
{
    public async IAsyncEnumerable<User> HandleAsync(
        GetAllUsersStreamQuery request, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var user in _repository.GetUsersByDepartmentAsync(request.Department))
        {
            yield return user; // Memory-efficient streaming
        }
    }
}

// Usage - process millions of records efficiently
await foreach (var user in _mediator.CreateStream(new GetAllUsersStreamQuery("Engineering")))
{
    await ProcessUserAsync(user); // Process each user as it arrives
}
```

### 3. Enterprise-Grade Resilience

Built-in patterns to handle failures gracefully:

```csharp
// Add resilience with simple configuration
builder.Services
    .AddFSMediator()
    .AddRetryBehavior(RetryPreset.Database)           // Intelligent database retry
    .AddCircuitBreakerBehavior(CircuitBreakerPreset.ExternalApi)  // API protection
    .AddLoggingBehavior()                             // Comprehensive logging
    .AddPerformanceBehavior();                        // Performance monitoring
```

### 4. Streaming with Resilience

Combine streaming with enterprise patterns for robust data processing:

```csharp
builder.Services
    .AddFSMediator()
    .AddStreamingResiliencePackage()                  // Complete streaming protection
    .AddStreamingBackpressureBehavior(BackpressurePreset.Analytics)  // Handle load spikes
    .AddStreamingHealthCheckBehavior(HealthCheckPreset.LongRunning); // Monitor health
```

## 📋 Comprehensive Examples

### Example 1: E-commerce Order Processing

```csharp
// Commands and Queries
public record CreateOrderCommand(int CustomerId, List<OrderItem> Items) : IRequest<OrderResult>;
public record GetOrderHistoryQuery(int CustomerId, int PageSize) : IStreamRequest<Order>;
public record OrderCreatedNotification(Order Order) : INotification;

// Handlers with built-in resilience
public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, OrderResult>
{
    public async Task<OrderResult> HandleAsync(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        // Business logic with automatic retry, circuit breaking, and logging
        var order = await _orderService.CreateOrderAsync(request.CustomerId, request.Items);
        
        // Publish notification - multiple handlers can process this
        await _mediator.PublishAsync(new OrderCreatedNotification(order));
        
        return new OrderResult(order.Id, order.Total);
    }
}

// Streaming for large datasets
public class GetOrderHistoryHandler : IStreamRequestHandler<GetOrderHistoryQuery, Order>
{
    public async IAsyncEnumerable<Order> HandleAsync(
        GetOrderHistoryQuery request, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Process orders one by one - memory efficient for large histories
        await foreach (var order in _repository.GetOrderHistoryStreamAsync(request.CustomerId))
        {
            yield return order;
        }
    }
}

// Multiple notification handlers
public class OrderCreatedEmailHandler : INotificationHandler<OrderCreatedNotification>
{
    public async Task HandleAsync(OrderCreatedNotification notification, CancellationToken cancellationToken)
    {
        await _emailService.SendOrderConfirmationAsync(notification.Order);
    }
}

public class OrderCreatedInventoryHandler : INotificationHandler<OrderCreatedNotification>
{
    public async Task HandleAsync(OrderCreatedNotification notification, CancellationToken cancellationToken)
    {
        await _inventoryService.UpdateStockAsync(notification.Order.Items);
    }
}
```

### Example 2: Real-time Data Processing

```csharp
// Configuration for high-throughput scenario
builder.Services
    .AddFSMediator()
    .AddStreamingPlatinumPackage(                     // Premium protection package
        resourcePreset: ResourceManagementPreset.HighPerformance,
        backpressurePreset: BackpressurePreset.RealTime)
    .AddStreamingHealthCheckBehavior<CustomHealthReporter>();

// Processing large datasets with full monitoring
public record ProcessSensorDataQuery(DateTime FromTime, string SensorType) : IStreamRequest<ProcessedReading>;

public class ProcessSensorDataHandler : IStreamRequestHandler<ProcessSensorDataQuery, ProcessedReading>
{
    public async IAsyncEnumerable<ProcessedReading> HandleAsync(
        ProcessSensorDataQuery request, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Stream millions of sensor readings with automatic:
        // - Memory management
        // - Performance monitoring  
        // - Health checking
        // - Backpressure handling
        // - Error recovery
        
        await foreach (var reading in _sensorRepository.GetReadingsStreamAsync(request.FromTime, request.SensorType))
        {
            var processed = await _processor.ProcessReadingAsync(reading);
            yield return processed;
        }
    }
}
```

## ⚙️ Configuration Reference

### Pipeline Behaviors

Pipeline behaviors provide cross-cutting concerns and execute in registration order:

```csharp
builder.Services
    .AddFSMediator()
    
    // Logging - always first for complete visibility
    .AddLoggingBehavior()
    
    // Performance monitoring
    .AddPerformanceBehavior(warningThresholdMs: 1000)
    
    // Resilience patterns
    .AddRetryBehavior(options => {
        options.MaxRetryAttempts = 3;
        options.Strategy = RetryStrategy.ExponentialBackoffWithJitter;
        options.MaxTotalRetryTime = TimeSpan.FromSeconds(30);
    })
    
    .AddCircuitBreakerBehavior(options => {
        options.FailureThresholdPercentage = 50.0;
        options.DurationOfBreak = TimeSpan.FromSeconds(30);
        options.MinimumThroughput = 5;
    })
    
    // Resource management
    .AddResourceManagementBehavior(ResourceManagementPreset.Balanced);
```

### Streaming Configuration

Configure streaming behaviors for different scenarios:

```csharp
// For data processing workloads
builder.Services
    .AddFSMediator()
    .AddStreamingLoggingBehavior(options => {
        options.LogProgressEveryNItems = 10000;      // Log every 10k items
        options.LogProgressEveryNSeconds = 60;       // Log every minute
    })
    .AddStreamingBackpressureBehavior(BackpressurePreset.Analytics)
    .AddStreamingResourceManagementBehavior(ResourceManagementPreset.MemoryConstrained);

// For real-time applications  
builder.Services
    .AddFSMediator()
    .AddStreamingBackpressureBehavior(BackpressurePreset.RealTime)
    .AddStreamingHealthCheckBehavior(HealthCheckPreset.HighPerformance);
```

### Backpressure Strategies

Choose the right strategy for your use case:

```csharp
// Buffer strategy (default) - queue items when consumer is slow
builder.Services.AddStreamingBackpressureBehavior(options => {
    options.Strategy = BackpressureStrategy.Buffer;
    options.MaxBufferSize = 10000;
});

// Drop strategy - discard items when overwhelmed
builder.Services.AddStreamingBackpressureBehavior(options => {
    options.Strategy = BackpressureStrategy.Drop;
    options.PreferNewerItems = true; // Keep latest data
});

// Throttle strategy - slow down producer to match consumer
builder.Services.AddStreamingBackpressureBehavior(options => {
    options.Strategy = BackpressureStrategy.Throttle;
    options.MaxThrottleDelayMs = 1000; // Max 1 second delay
});

// Sample strategy - process only subset of items under pressure
builder.Services.AddStreamingBackpressureBehavior(options => {
    options.Strategy = BackpressureStrategy.Sample;
    options.SampleRate = 2; // Process every 2nd item under pressure
});

// Block strategy - halt producer until consumer catches up
builder.Services.AddStreamingBackpressureBehavior(options => {
    options.Strategy = BackpressureStrategy.Block;
});
```

### Request/Response Interceptors

Add cross-cutting concerns with surgical precision:

```csharp
// Custom request interceptor
public class SecurityInterceptor<TRequest, TResponse> : IRequestInterceptor<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TRequest> InterceptRequestAsync(TRequest request, CancellationToken cancellationToken)
    {
        // Add security checks, audit logging, etc.
        await _securityService.ValidateRequestAsync(request);
        return request;
    }
}

// Register interceptors
builder.Services
    .AddFSMediator()
    .AddRequestInterceptor<GetSensitiveDataQuery, SensitiveData, SecurityInterceptor<GetSensitiveDataQuery, SensitiveData>>();
```

## 🔧 Advanced Features

### Custom Health Monitoring

Implement custom health reporting for your monitoring infrastructure:

```csharp
public class ApplicationInsightsHealthReporter : IStreamHealthReporter
{
    public async Task ReportHealthAsync(StreamHealthMetrics metrics, CancellationToken cancellationToken)
    {
        _telemetryClient.TrackMetric("StreamThroughput", metrics.CurrentThroughput);
        _telemetryClient.TrackMetric("StreamMemoryUsage", metrics.CurrentMemoryUsage);
    }

    public async Task ReportCriticalIssueAsync(StreamHealthMetrics metrics, HealthWarning warning, CancellationToken cancellationToken)
    {
        _telemetryClient.TrackException(new Exception($"Stream health critical: {warning.Message}"));
    }
}

// Register custom health reporter
builder.Services.AddStreamingHealthCheckBehavior<ApplicationInsightsHealthReporter>();
```

### Custom Backpressure Strategies

Define custom logic for handling producer-consumer speed mismatches:

```csharp
builder.Services.AddStreamingBackpressureBehavior(options => {
    options.Strategy = BackpressureStrategy.Buffer; // Use existing strategy
    options.CustomBackpressureHandler = context => {
        if (context.Metrics.CurrentBufferSize > 50000)
        {
            // Custom logic: maybe alert operations team
            _alertService.SendAlert("High backpressure detected");
            
            // Or dynamically scale resources
            _scalingService.ScaleOutAsync();
        }
    };
});
```

## 📊 Available Presets

### BackpressurePreset Options

```csharp
BackpressurePreset.NoDataLoss      // Prioritizes data completeness
BackpressurePreset.HighThroughput  // Maximizes performance
BackpressurePreset.MemoryConstrained // For limited memory environments
BackpressurePreset.RealTime        // For real-time applications
BackpressurePreset.Analytics       // For data processing/analytics
BackpressurePreset.Balanced        // General purpose
```

### HealthCheckPreset Options

```csharp
HealthCheckPreset.HighPerformance   // Real-time monitoring
HealthCheckPreset.DataProcessing    // Batch operations
HealthCheckPreset.LongRunning       // Overnight jobs
HealthCheckPreset.RealTime          // User-facing streams
HealthCheckPreset.Development       // Testing/debugging
```

### ResourceManagementPreset Options

```csharp
ResourceManagementPreset.MemoryConstrained  // Containers/embedded
ResourceManagementPreset.HighPerformance    // Performance critical
ResourceManagementPreset.Balanced           // Most applications
ResourceManagementPreset.Development        // Debugging scenarios
```

### CircuitBreakerPreset Options

```csharp
CircuitBreakerPreset.Sensitive     // Quick failure detection
CircuitBreakerPreset.Balanced      // General purpose
CircuitBreakerPreset.Resilient     // High failure tolerance
CircuitBreakerPreset.Database      // Database operations
CircuitBreakerPreset.ExternalApi   // External service calls
```

### RetryPreset Options

```csharp
RetryPreset.Conservative   // Quick failure, minimal retries
RetryPreset.Aggressive     // Persistent retries
RetryPreset.Database       // Database-specific handling
RetryPreset.HttpApi        // HTTP/network operations
```

## 📊 Performance Characteristics

FS.Mediator is designed for high-performance scenarios:

| Feature | Performance Impact | Memory Usage | Best For |
|---------|-------------------|--------------|----------|
| Basic Mediator | < 1μs overhead | Minimal | All scenarios |
| Streaming | ~2-5μs per item | O(1) constant | Large datasets |
| Retry Behavior | Varies by strategy | Minimal | Unreliable services |
| Circuit Breaker | < 1μs when closed | ~1KB per type | External dependencies |
| Backpressure | Adaptive | Configurable | High-throughput streams |

### Benchmarks

Real-world performance metrics on a standard development machine:

```
// Basic request/response
BenchmarkDotNet results:
|     Method |      Mean |   Error |  StdDev |
|----------- |----------:|--------:|--------:|
| SimpleRequest | 1.2 μs | 0.02 μs | 0.02 μs |

// Streaming throughput  
Items processed: 1,000,000
Total time: 2.3 seconds
Throughput: 434,782 items/second
Memory usage: 45 MB (constant)
```

## 🚧 Troubleshooting

### Common Issues

**Issue**: Handlers not found
```
HandlerNotFoundException: No handler found for type 'MyRequest'
```
**Solution**: Ensure your handlers are in assemblies passed to `AddFSMediator()`:
```csharp
builder.Services.AddFSMediator(typeof(MyHandler).Assembly);
```

**Issue**: Streaming performance problems
```
Stream throughput below expected levels
```
**Solution**: Configure backpressure and resource management:
```csharp
builder.Services
    .AddFSMediator()
    .AddStreamingBackpressureBehavior(BackpressurePreset.HighThroughput)
    .AddStreamingResourceManagementBehavior(ResourceManagementPreset.HighPerformance);
```

**Issue**: Memory usage growing during streaming
```
OutOfMemoryException during large stream processing
```
**Solution**: Enable resource management with appropriate presets:
```csharp
builder.Services
    .AddFSMediator()
    .AddStreamingResourceManagementBehavior(ResourceManagementPreset.MemoryConstrained);
```

### Best Practices

1. **Always use streaming for large datasets** - Don't load millions of records into memory
2. **Configure resilience patterns early** - Add retry and circuit breaker behaviors from the start
3. **Monitor your streams** - Use health check behaviors in production
4. **Choose appropriate presets** - Start with presets, customize only when needed
5. **Test backpressure scenarios** - Simulate high load to validate your configuration

### Debugging Tips

Enable detailed logging to understand behavior execution:

```csharp
builder.Services
    .AddFSMediator()
    .AddLoggingBehavior()  // Logs all requests/responses
    .AddStreamingLoggingBehavior(options => {
        options.LogProgressEveryNItems = 1000;      // Progress updates
        options.LogDetailedMetrics = true;          // Performance data
    });
```

## 📚 Documentation

For more detailed documentation, see:

- [Getting Started Guide](docs/getting-started.md)
- [Streaming Operations](docs/streaming/overview.md)
- [Resilience Patterns](docs/resilience/overview.md)
- [Configuration Reference](docs/configuration/behaviors.md)
- [Performance Tuning](docs/streaming/performance-tips.md)
- [Examples Repository](docs/examples/)

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

### Development Setup

```bash
git clone https://github.com/furkansarikaya/FS.Mediator.git
cd FS.Mediator
dotnet restore
dotnet build
dotnet test
```

## 📦 Packages

| Package | Description | NuGet |
|---------|-------------|-------|
| FS.Mediator | Core library with all features | [![NuGet](https://img.shields.io/nuget/v/FS.Mediator.svg)](https://www.nuget.org/packages/FS.Mediator/) |

## 🗺️ Roadmap

- [ ] **Performance Improvements**: Zero-allocation streaming paths
- [ ] **Additional Patterns**: Saga pattern support for complex workflows
- [ ] **Observability**: OpenTelemetry integration
- [ ] **Cloud Native**: Kubernetes health checks integration
- [ ] **AI/ML**: Streaming ML pipeline support

## 🌟 Star History

If you find this library useful, please consider giving it a star on GitHub! It helps others discover the project.

**Made with ❤️ by [Furkan Sarıkaya](https://github.com/furkansarikaya)**

[![GitHub](https://img.shields.io/badge/github-%23121011.svg?style=for-the-badge&logo=github&logoColor=white)](https://github.com/furkansarikaya)
[![LinkedIn](https://img.shields.io/badge/linkedin-%230077B5.svg?style=for-the-badge&logo=linkedin&logoColor=white)](https://www.linkedin.com/in/furkansarikaya/)
[![Medium](https://img.shields.io/badge/medium-%23121011.svg?style=for-the-badge&logo=medium&logoColor=white)](https://medium.com/@furkansarikaya)

---

## Support

If you encounter any issues or have questions:

1. Check the [troubleshooting section](#troubleshooting)
2. Search existing [GitHub issues](https://github.com/furkansarikaya/FS.Mediator/issues)
3. Create a new issue with detailed information
4. Join our community discussions

**Happy coding! 🚀**