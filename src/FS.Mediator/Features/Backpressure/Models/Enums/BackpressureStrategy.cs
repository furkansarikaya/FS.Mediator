namespace FS.Mediator.Features.Backpressure.Models.Enums;

/// <summary>
/// Defines different strategies for handling backpressure when consumers can't keep up with producers.
/// Each strategy represents a different trade-off between resource usage, data loss, and system stability.
/// </summary>
public enum BackpressureStrategy
{
    /// <summary>
    /// Buffer items in memory until consumer catches up.
    /// 
    /// **Pros:**
    /// - No data loss - all items eventually processed
    /// - Simple to understand and implement
    /// - Good for temporary spikes in load
    /// 
    /// **Cons:**
    /// - Can consume unlimited memory
    /// - Risk of OutOfMemoryException
    /// - Latency increases as buffer grows
    /// 
    /// **Best for:**
    /// - Systems with predictable memory limits
    /// - Temporary load spikes (not sustained overload)
    /// - Critical data that cannot be lost
    /// - Systems with fast consumers that just need smoothing
    /// 
    /// **Example:** Processing payment transactions - every transaction is important
    /// </summary>
    Buffer,
    
    /// <summary>
    /// Drop items when buffer capacity is exceeded.
    /// 
    /// **Pros:**
    /// - Bounded memory usage
    /// - System remains stable under any load
    /// - Low latency for items that are processed
    /// 
    /// **Cons:**
    /// - Data loss when overwhelmed
    /// - May lose important information
    /// - Requires careful monitoring of drop rates
    /// 
    /// **Best for:**
    /// - Real-time systems where freshness matters more than completeness
    /// - Monitoring/telemetry data with redundancy
    /// - Systems where some data loss is acceptable
    /// 
    /// **Example:** Server monitoring metrics - dropping a few data points is okay
    /// </summary>
    Drop,
    
    /// <summary>
    /// Slow down the producer by introducing delays.
    /// 
    /// **Pros:**
    /// - No data loss
    /// - Bounded memory usage
    /// - Automatically adjusts to consumer capacity
    /// 
    /// **Cons:**
    /// - Reduces overall throughput
    /// - May cause timeouts in producer
    /// - Can create cascading delays in distributed systems
    /// 
    /// **Best for:**
    /// - Systems where producer can tolerate delays
    /// - Batch processing scenarios
    /// - When data completeness is critical but timing is flexible
    /// 
    /// **Example:** ETL data processing - better to process slowly than lose data
    /// </summary>
    Throttle,
    
    /// <summary>
    /// Process only a subset of items when under pressure.
    /// 
    /// **Pros:**
    /// - Bounded memory usage
    /// - Maintains system responsiveness
    /// - Good for statistical/approximate processing
    /// 
    /// **Cons:**
    /// - Data loss (by design)
    /// - May miss important items
    /// - Requires careful selection of which items to process
    /// 
    /// **Best for:**
    /// - Analytics where sampling is statistically valid
    /// - Systems with highly redundant data
    /// - When approximate results are acceptable
    /// 
    /// **Example:** Website analytics - processing 1 in 10 page views still gives valid insights
    /// </summary>
    Sample,
    
    /// <summary>
    /// Block the producer completely until consumer catches up.
    /// 
    /// **Pros:**
    /// - No data loss
    /// - Simple backpressure mechanism
    /// - Clear producer-consumer coordination
    /// 
    /// **Cons:**
    /// - Can cause producer timeouts
    /// - May create deadlocks in some scenarios
    /// - Reduces system throughput significantly
    /// 
    /// **Best for:**
    /// - Synchronous processing scenarios
    /// - When data order and completeness are critical
    /// - Systems with well-coordinated producers and consumers
    /// 
    /// **Example:** Database replication - ensure all changes are processed in order
    /// </summary>
    Block
}