namespace FS.Mediator.Models.Options;

/// <summary>
/// Configuration options for streaming logging behavior.
/// 
/// Streaming operations can produce thousands or millions of items, so logging
/// every single item would overwhelm your logs and degrade performance.
/// These options help you find the right balance between visibility and performance.
/// </summary>
public class StreamingLoggingOptions
{
    /// <summary>
    /// Gets or sets how often to log progress based on item count.
    /// For example, setting this to 1000 will log progress every 1000 items.
    /// Set to 0 to disable item-count-based progress logging.
    /// Default is 1000 items.
    /// 
    /// Use lower values (100-500) for debugging, higher values (5000-10000) for production.
    /// </summary>
    public int LogProgressEveryNItems { get; set; } = 1000;
    
    /// <summary>
    /// Gets or sets how often to log progress based on time intervals (in seconds).
    /// For example, setting this to 30 will log progress every 30 seconds regardless of item count.
    /// Set to 0 to disable time-based progress logging.
    /// Default is 30 seconds.
    /// 
    /// This is crucial for slow streams that might not hit the item count threshold quickly.
    /// </summary>
    public int LogProgressEveryNSeconds { get; set; } = 30;
    
    /// <summary>
    /// Gets or sets whether to log detailed performance metrics at stream completion.
    /// This includes items per second, total duration, and memory usage patterns.
    /// Default is true.
    /// 
    /// Disable this in high-frequency scenarios where the logging overhead becomes significant.
    /// </summary>
    public bool LogDetailedMetrics { get; set; } = true;
}