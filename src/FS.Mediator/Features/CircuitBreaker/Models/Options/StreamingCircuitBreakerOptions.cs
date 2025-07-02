namespace FS.Mediator.Features.CircuitBreaker.Models.Options;

/// <summary>
/// Configuration options for streaming circuit breaker behavior.
/// 
/// Circuit breakers for streaming operations need to consider different failure patterns:
/// 1. Stream-level failures (entire stream fails to start or complete)
/// 2. Item-level failures (individual items fail processing)
/// 3. Partial success scenarios (stream yields many items then fails)
/// 
/// This configuration focuses on stream-level circuit breaking, which protects
/// downstream services from cascade failures caused by problematic streams.
/// </summary>
public class StreamingCircuitBreakerOptions
{
    /// <summary>
    /// Gets or sets the failure threshold percentage for stream operations.
    /// This represents the percentage of streams that must fail before the circuit opens.
    /// Default is 60% (higher than regular requests because partial stream success is valuable).
    /// 
    /// Streams are different from regular requests because even a "failed" stream might
    /// have yielded valuable data before failing. Consider this when setting thresholds.
    /// </summary>
    public double FailureThresholdPercentage { get; set; } = 60.0;
    
    /// <summary>
    /// Gets or sets the minimum number of stream attempts required before the circuit can open.
    /// This prevents the circuit from opening due to a small number of failures when traffic is low.
    /// Default is 3 streams.
    /// 
    /// This is typically lower than regular requests because streams are often less frequent
    /// but more expensive operations.
    /// </summary>
    public int MinimumThroughput { get; set; } = 3;
    
    /// <summary>
    /// Gets or sets the time window for collecting stream failure statistics.
    /// The circuit breaker tracks failures within this rolling window.
    /// Default is 5 minutes (longer than regular requests because streams run longer).
    /// 
    /// Streaming operations typically have longer lifecycles than regular requests,
    /// so we need a longer observation window to make meaningful decisions.
    /// </summary>
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// Gets or sets how long the circuit stays open before testing recovery.
    /// During this time, all stream requests fail immediately without attempting execution.
    /// Default is 2 minutes (longer recovery time for stream operations).
    /// 
    /// Streaming failures often indicate deeper system issues (database problems,
    /// network partitions) that take longer to resolve than typical request failures.
    /// </summary>
    public TimeSpan DurationOfBreak { get; set; } = TimeSpan.FromMinutes(2);
    
    /// <summary>
    /// Gets or sets the number of trial streams allowed in the half-open state.
    /// When testing recovery, only this many streams are allowed through.
    /// Default is 2 streams.
    /// 
    /// Fewer trial streams for testing because each stream might be expensive to execute.
    /// We want to test recovery without overwhelming a potentially still-fragile system.
    /// </summary>
    public int TrialRequestCount { get; set; } = 2;
    
    /// <summary>
    /// Gets or sets the minimum number of items a stream must yield to be considered partially successful.
    /// Streams that yield at least this many items before failing might be treated as partial successes.
    /// Default is 0 (disabled - all failures are treated equally).
    /// 
    /// Set this to a meaningful number for your use case. For example, if you're processing
    /// user records and yielding at least 1000 users is valuable even if the stream later fails,
    /// set this to 1000.
    /// </summary>
    public long PartialSuccessThreshold { get; set; } = 0;
    
    /// <summary>
    /// Gets or sets a function to determine if an exception should be counted as a circuit breaker failure.
    /// This allows you to distinguish between different types of failures.
    /// 
    /// Default behavior counts all exceptions as failures, but you might want to exclude
    /// business logic exceptions or validation errors that don't indicate system health issues.
    /// </summary>
    public Func<Exception, bool> ShouldCountAsFailure { get; set; } = _ => true;
}