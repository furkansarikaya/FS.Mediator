using FS.Mediator.Features.Backpressure.Models.Enums;

namespace FS.Mediator.Features.Backpressure.Models.Options;

/// <summary>
/// Configuration options for backpressure handling in streaming operations.
/// 
/// Backpressure is like traffic management for your data streams. Imagine you're running
/// a restaurant kitchen where orders come in faster than the chefs can prepare them.
/// You have several options:
/// 
/// 1. **Buffer Strategy**: Keep a reasonable queue of orders (but not unlimited)
/// 2. **Drop Strategy**: Start rejecting new orders when overwhelmed  
/// 3. **Throttle Strategy**: Slow down accepting new orders to match cooking speed
/// 4. **Sample Strategy**: Only process every nth order when under pressure
/// 
/// Each strategy has trade-offs between resource usage, data completeness, and system stability.
/// The key insight is that doing something intentional is always better than letting the
/// system crash from resource exhaustion.
/// </summary>
public class BackpressureOptions
{
    /// <summary>
    /// Gets or sets the backpressure strategy to use when consumer cannot keep up with producer.
    /// This is your fundamental decision about how to handle overwhelming data flows.
    /// Default is Buffer - safe but memory-intensive.
    /// 
    /// Think of this as choosing your "traffic management" philosophy:
    /// - Buffer: "Let's queue them up and hope traffic clears"
    /// - Drop: "Turn away cars when the road is full"
    /// - Throttle: "Control the traffic lights to manage flow"
    /// - Sample: "Only let every 3rd car through during rush hour"
    /// </summary>
    public BackpressureStrategy Strategy { get; set; } = BackpressureStrategy.Buffer;
    
    /// <summary>
    /// Gets or sets the maximum number of items to buffer before applying backpressure.
    /// This acts as your "early warning system" - when the buffer reaches this size,
    /// backpressure strategies kick in. Default is 10,000 items.
    /// 
    /// Setting this value requires understanding your data:
    /// - Small objects (strings, numbers): 50,000-100,000 items
    /// - Medium objects (small DTOs): 10,000-25,000 items  
    /// - Large objects (images, documents): 1,000-5,000 items
    /// - Huge objects (videos, large files): 100-1,000 items
    /// 
    /// Remember: It's better to trigger backpressure early than to run out of memory!
    /// </summary>
    public int MaxBufferSize { get; set; } = 10_000;
    
    /// <summary>
    /// Gets or sets the high watermark threshold (as percentage of MaxBufferSize) where backpressure begins.
    /// This provides a "yellow light" warning before hitting the "red light" maximum.
    /// Default is 80% - start applying pressure before the buffer is completely full.
    /// 
    /// Example: With MaxBufferSize=10,000 and HighWaterMark=0.8, backpressure starts at 8,000 items.
    /// This gives your system time to react gracefully rather than hitting a hard wall.
    /// </summary>
    public double HighWaterMarkThreshold { get; set; } = 0.8; // 80%
    
    /// <summary>
    /// Gets or sets the low watermark threshold (as percentage of MaxBufferSize) where backpressure relief begins.
    /// This provides hysteresis - once backpressure starts, we don't stop it immediately when buffer drops.
    /// Default is 50% - only relieve pressure when buffer is significantly reduced.
    /// 
    /// This prevents "flapping" - rapidly turning backpressure on and off, which can cause instability.
    /// Think of it like a thermostat with a dead zone to prevent constant cycling.
    /// </summary>
    public double LowWaterMarkThreshold { get; set; } = 0.5; // 50%
    
    /// <summary>
    /// Gets or sets the maximum delay to introduce when throttling producers (in milliseconds).
    /// This is your "brake pedal" - how hard you slow down the producer when needed.
    /// Default is 1000ms (1 second) maximum delay.
    /// 
    /// Throttling works by adding delays to producer operations:
    /// - Light pressure: 10-100ms delays (barely noticeable)
    /// - Medium pressure: 100-500ms delays (noticeable but tolerable)
    /// - Heavy pressure: 500-1000ms delays (significant but prevents crash)
    /// 
    /// Set this based on your latency requirements and user expectations.
    /// </summary>
    public int MaxThrottleDelayMs { get; set; } = 1000;
    
    /// <summary>
    /// Gets or sets the sampling rate when using the Sample strategy (1 = process all, 2 = every other, 3 = every third, etc.).
    /// This is your "data diet" - when overwhelmed, only process a fraction of incoming data.
    /// Default is 2 (process every other item).
    /// 
    /// Sampling is useful when:
    /// - Data is redundant (multiple sensors reporting similar readings)
    /// - Approximate results are acceptable (analytics, monitoring)
    /// - System stability is more important than data completeness
    /// 
    /// Example: SampleRate=5 means process 1 out of every 5 items (20% of data)
    /// </summary>
    public int SampleRate { get; set; } = 2;
    
    /// <summary>
    /// Gets or sets the time window for measuring producer vs consumer rates (in seconds).
    /// This is your "measurement window" for detecting when backpressure is needed.
    /// Default is 30 seconds - long enough to be meaningful, short enough to be responsive.
    /// 
    /// Shorter windows (5-15 seconds):
    /// - More responsive to sudden spikes
    /// - May trigger false alarms from temporary fluctuations
    /// 
    /// Longer windows (60-300 seconds):
    /// - More stable, avoids false alarms
    /// - Less responsive to genuine problems
    /// 
    /// The sweet spot is usually 15-60 seconds depending on your data patterns.
    /// </summary>
    public int MeasurementWindowSeconds { get; set; } = 30;
    
    /// <summary>
    /// Gets or sets whether to prioritize newer or older items when dropping under pressure.
    /// true = drop older items (tail drop), false = drop newer items (head drop).
    /// Default is true - keep the most recent data.
    /// 
    /// This is a crucial business decision:
    /// 
    /// **Drop Older Items (true)**:
    /// - Best for real-time data (stock prices, sensor readings)
    /// - Ensures you always have the latest information
    /// - Example: Keep the most recent temperature readings, discard old ones
    /// 
    /// **Drop Newer Items (false)**:
    /// - Best for historical/sequential data (log processing, event streams)
    /// - Maintains chronological order and completeness
    /// - Example: Process log entries in order, reject new ones if overwhelmed
    /// </summary>
    public bool PreferNewerItems { get; set; } = true;
    
    /// <summary>
    /// Gets or sets whether to enable adaptive backpressure that adjusts strategy based on conditions.
    /// When enabled, the system can automatically switch between strategies for optimal performance.
    /// Default is false - use a single consistent strategy.
    /// 
    /// Adaptive backpressure is like having a smart traffic management system that:
    /// - Uses different strategies based on traffic patterns
    /// - Learns from historical data to make better decisions
    /// - Automatically adjusts thresholds based on system performance
    /// 
    /// Enable this for:
    /// - Variable workloads with unpredictable patterns
    /// - Systems that need to optimize for different objectives over time
    /// - Production systems with sophisticated monitoring
    /// 
    /// Keep disabled for:
    /// - Predictable workloads
    /// - Systems where consistency is more important than optimization
    /// - Development/testing environments
    /// </summary>
    public bool EnableAdaptiveBackpressure { get; set; } = false;
    
    /// <summary>
    /// Gets or sets custom logic for determining when to apply backpressure.
    /// This allows you to implement domain-specific backpressure triggers beyond simple buffer size.
    /// 
    /// Example custom triggers:
    /// - Memory usage exceeds threshold
    /// - CPU utilization too high
    /// - Database connection pool exhausted
    /// - External API rate limits approaching
    /// - Business rules (market closed, maintenance window)
    /// 
    /// The function receives current metrics and should return true to trigger backpressure.
    /// </summary>
    public Func<BackpressureMetrics, bool>? CustomBackpressureTrigger { get; set; }
    
    /// <summary>
    /// Gets or sets custom logic for handling backpressure situations.
    /// This allows you to implement domain-specific responses beyond the standard strategies.
    /// 
    /// Example custom handlers:
    /// - Notify administrators of the situation
    /// - Switch to alternative data sources
    /// - Trigger auto-scaling in cloud environments
    /// - Store overflow data to persistent storage
    /// - Update monitoring dashboards
    /// </summary>
    public Action<BackpressureContext>? CustomBackpressureHandler { get; set; }
    
    /// <summary>
    /// Gets or sets whether to collect detailed metrics about backpressure events.
    /// This provides valuable insights for tuning and monitoring but adds overhead.
    /// Default is true - metrics are crucial for understanding backpressure behavior.
    /// 
    /// Detailed metrics include:
    /// - Frequency and duration of backpressure events
    /// - Effectiveness of different strategies
    /// - Producer vs consumer rate comparisons
    /// - Buffer utilization patterns
    /// - Impact on system performance
    /// 
    /// This data is essential for:
    /// - Optimizing backpressure configuration
    /// - Capacity planning
    /// - Understanding system behavior under load
    /// - Troubleshooting performance issues
    /// </summary>
    public bool CollectDetailedMetrics { get; set; } = true;
}