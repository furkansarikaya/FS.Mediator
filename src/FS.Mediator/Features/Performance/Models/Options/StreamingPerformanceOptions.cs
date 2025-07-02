namespace FS.Mediator.Features.Performance.Models.Options;

/// <summary>
/// Configuration options for streaming performance monitoring behavior.
/// 
/// Performance monitoring for streams requires different metrics than regular requests:
/// - Throughput (items per second) instead of just response time
/// - Time to first item (latency) separate from total duration
/// - Progress tracking for long-running operations
/// 
/// These options help you identify performance bottlenecks in streaming scenarios.
/// </summary>
public class StreamingPerformanceOptions
{
    /// <summary>
    /// Gets or sets the warning threshold for time to first item (in milliseconds).
    /// This measures how long it takes for a stream to yield its first item after being started.
    /// Default is 5000ms (5 seconds).
    /// 
    /// This is crucial for user experience - even if the total stream takes a long time,
    /// users expect to see the first results quickly. High time-to-first-item often
    /// indicates initialization problems (slow database queries, network delays, etc.).
    /// </summary>
    public int TimeToFirstItemWarningMs { get; set; } = 5000;
    
    /// <summary>
    /// Gets or sets the minimum expected throughput in items per second.
    /// Streams that consistently produce fewer items per second trigger performance warnings.
    /// Default is 10 items per second.
    /// 
    /// Set this based on your specific use case:
    /// - Database query streams might target 100-1000 items/sec
    /// - API aggregation streams might target 10-50 items/sec  
    /// - File processing streams might target 500-5000 items/sec
    /// </summary>
    public double MinimumThroughputItemsPerSecond { get; set; } = 10.0;
    
    /// <summary>
    /// Gets or sets how often to check throughput performance (in seconds).
    /// The behavior will calculate and check throughput at these intervals.
    /// Default is 30 seconds.
    /// 
    /// More frequent checks (10-15 seconds) provide faster feedback but generate more logs.
    /// Less frequent checks (60-120 seconds) reduce overhead but might miss short-term performance issues.
    /// </summary>
    public int ThroughputCheckIntervalSeconds { get; set; } = 30;
    
    /// <summary>
    /// Gets or sets the warning threshold for total stream duration (in milliseconds).
    /// Streams that run longer than this threshold trigger performance warnings.
    /// Default is 300,000ms (5 minutes).
    /// 
    /// This helps identify streams that are taking unexpectedly long to complete.
    /// Set this based on your business requirements and user expectations.
    /// </summary>
    public int TotalDurationWarningMs { get; set; } = 300_000; // 5 minutes
    
    /// <summary>
    /// Gets or sets whether to collect detailed memory usage metrics during streaming.
    /// This can help identify memory leaks or excessive memory usage in stream processing.
    /// Default is false (disabled due to performance overhead).
    /// 
    /// Enable this only when investigating specific performance issues, as it adds
    /// measurement overhead to every stream operation.
    /// </summary>
    public bool CollectMemoryMetrics { get; set; } = false;
}