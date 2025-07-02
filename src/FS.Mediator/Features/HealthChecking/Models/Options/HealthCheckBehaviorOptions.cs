namespace FS.Mediator.Features.HealthChecking.Models.Options;

/// <summary>
/// Configuration options for the health check behavior.
/// These settings control how aggressively the health checking system monitors
/// the stream and what thresholds trigger warnings.
/// </summary>
public class HealthCheckBehaviorOptions
{
    /// <summary>
    /// How often to perform comprehensive health assessments (in seconds).
    /// More frequent checks provide better responsiveness but use more CPU.
    /// Default: 10 seconds - a good balance for most scenarios.
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 10;
    
    /// <summary>
    /// Maximum time without new items before considering the stream stalled (in seconds).
    /// This should be set based on your expected stream characteristics.
    /// Default: 30 seconds - appropriate for most data processing scenarios.
    /// </summary>
    public int StallDetectionThresholdSeconds { get; set; } = 30;
    
    /// <summary>
    /// Maximum memory growth (in bytes) before triggering a warning.
    /// This helps detect memory leaks in long-running streams.
    /// Default: 100MB - appropriate for most applications.
    /// </summary>
    public long MemoryGrowthThresholdBytes { get; set; } = 100_000_000;
    
    /// <summary>
    /// Minimum expected throughput (items per second) after initial startup.
    /// Streams performing below this rate will trigger performance warnings.
    /// Default: 1.0 items/second - adjust based on your performance expectations.
    /// </summary>
    public double MinimumThroughputItemsPerSecond { get; set; } = 1.0;
    
    /// <summary>
    /// Maximum acceptable error rate (0.0 to 1.0) before triggering warnings.
    /// Default: 0.1 (10%) - adjust based on your quality requirements.
    /// </summary>
    public double MaximumErrorRate { get; set; } = 0.1;
    
    /// <summary>
    /// Whether to automatically trigger garbage collection when memory usage is high.
    /// This can help with memory pressure but may impact performance.
    /// Default: false - let the GC handle its own timing.
    /// </summary>
    public bool AutoTriggerGarbageCollection { get; set; } = false;
    
    /// <summary>
    /// Whether to include detailed memory statistics in health reports.
    /// This provides more diagnostic information but uses additional resources.
    /// Default: false - only enable for troubleshooting scenarios.
    /// </summary>
    public bool IncludeDetailedMemoryStats { get; set; } = false;
}