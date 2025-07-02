namespace FS.Mediator.Features.HealthChecking.Models.Enums;

/// <summary>
/// Predefined health check configurations optimized for common streaming scenarios.
/// 
/// These presets represent battle-tested configurations that have been optimized
/// for different types of streaming operations. Each preset balances monitoring
/// comprehensiveness with performance impact, providing sensible defaults for
/// specific use cases.
/// 
/// Think of these as "doctor visit types" - just like you'd have different
/// check-up procedures for a routine physical vs. monitoring a critical patient,
/// different streaming scenarios need different monitoring approaches.
/// </summary>
public enum HealthCheckPreset
{
    /// <summary>
    /// Optimized for high-performance, real-time streaming operations.
    /// 
    /// Best for: Financial trading systems, real-time analytics, live data feeds
    /// 
    /// Characteristics:
    /// - Very frequent health checks (every 5 seconds)
    /// - Quick stall detection (10 seconds)
    /// - High throughput expectations (1000+ items/second)
    /// - Aggressive memory management
    /// - Very low error tolerance (1%)
    /// 
    /// Trade-offs: Higher monitoring overhead but maximum responsiveness to issues
    /// </summary>
    HighPerformance,
    
    /// <summary>
    /// Optimized for batch data processing operations like ETL, data migration, and bulk imports.
    /// 
    /// Best for: Data warehouse ETL, database migrations, bulk data transformations
    /// 
    /// Characteristics:
    /// - Moderate health check frequency (every 30 seconds)
    /// - Longer stall tolerance (2 minutes) for complex processing
    /// - Moderate throughput expectations (50+ items/second)
    /// - Larger memory growth tolerance for data-intensive operations
    /// - Moderate error tolerance (5%) for data quality issues
    /// - Detailed memory statistics for optimization
    /// 
    /// Trade-offs: Balanced monitoring that doesn't interfere with data processing
    /// </summary>
    DataProcessing,
    
    /// <summary>
    /// Optimized for long-running, overnight batch jobs and maintenance operations.
    /// 
    /// Best for: Nightly reports, backup operations, system maintenance tasks
    /// 
    /// Characteristics:
    /// - Infrequent health checks (every minute) for minimal overhead
    /// - Long stall tolerance (5 minutes) for complex operations
    /// - Low throughput expectations (10+ items/second)
    /// - Large memory growth tolerance for long operations
    /// - Higher error tolerance (10%) for non-critical operations
    /// - Comprehensive diagnostics for post-analysis
    /// 
    /// Trade-offs: Minimal performance impact but slower issue detection
    /// </summary>
    LongRunning,
    
    /// <summary>
    /// Optimized for real-time, user-facing streaming operations.
    /// 
    /// Best for: Live dashboards, streaming APIs, user notifications, chat systems
    /// 
    /// Characteristics:
    /// - Very frequent health checks (every 2 seconds)
    /// - Immediate stall detection (5 seconds)
    /// - Consistent throughput expectations (100+ items/second)
    /// - Very low memory tolerance for responsive UX
    /// - Extremely low error tolerance (0.1%) for user experience
    /// - Proactive memory management
    /// 
    /// Trade-offs: Higher overhead but essential for user-facing reliability
    /// </summary>
    RealTime,
    
    /// <summary>
    /// Optimized for development, testing, and debugging scenarios.
    /// 
    /// Best for: Local development, integration testing, debugging streaming issues
    /// 
    /// Characteristics:
    /// - Moderate health check frequency (every 10 seconds)
    /// - Reasonable stall detection (30 seconds)
    /// - Very low throughput requirements (1+ items/second)
    /// - High error tolerance (20%) for testing error scenarios
    /// - Comprehensive diagnostics for debugging
    /// - Predictable behavior for testing
    /// 
    /// Trade-offs: Prioritizes debugging capability over performance optimization
    /// </summary>
    Development
}