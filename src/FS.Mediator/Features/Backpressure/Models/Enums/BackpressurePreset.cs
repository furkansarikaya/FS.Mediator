namespace FS.Mediator.Features.Backpressure.Models.Enums;

/// <summary>
/// Predefined backpressure configurations optimized for different data processing scenarios.
/// 
/// These presets represent different "traffic management philosophies" for handling
/// situations where data producers are faster than consumers. Each preset embodies
/// a different set of trade-offs between throughput, latency, data completeness,
/// and resource usage.
/// 
/// Think of these as choosing the right traffic management strategy for different
/// types of roads and traffic patterns.
/// </summary>
public enum BackpressurePreset
{
    /// <summary>
    /// Prioritizes data completeness - no data should ever be lost.
    /// 
    /// This preset is like having a "data preservation specialist" who treats
    /// every piece of information as precious and irreplaceable. Performance
    /// may suffer, but data integrity is paramount.
    /// 
    /// Strategy: Throttle with large buffers
    /// Trade-offs: Maximum data completeness, higher latency, more memory usage
    /// 
    /// Configuration highlights:
    /// - Uses Throttle strategy to slow down producers
    /// - Large buffer sizes (50,000 items)
    /// - High watermark threshold (90%)
    /// - Accepts significant delays (up to 5 seconds)
    /// - Detailed metrics for monitoring
    /// 
    /// Best for:
    /// - Financial transaction processing
    /// - Critical business data pipelines
    /// - Audit logging systems
    /// - Medical records processing
    /// - Any system where data loss is unacceptable
    /// </summary>
    NoDataLoss,
    
    /// <summary>
    /// Optimized for maximum throughput and system responsiveness.
    /// 
    /// This preset is like having a "performance-focused traffic manager" who
    /// keeps traffic flowing smoothly even if it means some cars have to take
    /// alternate routes. Speed and responsiveness are prioritized over completeness.
    /// 
    /// Strategy: Drop with moderate buffers and early intervention
    /// Trade-offs: Highest throughput, lowest latency, potential data loss
    /// 
    /// Configuration highlights:
    /// - Uses Drop strategy to maintain flow
    /// - Moderate buffer sizes (10,000 items)
    /// - Early intervention (70% watermark)
    /// - Prefers newer items over older ones
    /// - Detailed metrics for optimization
    /// 
    /// Best for:
    /// - Real-time monitoring systems
    /// - Live dashboards and analytics
    /// - High-frequency sensor data
    /// - Social media feeds
    /// - Systems where approximate data is sufficient
    /// </summary>
    HighThroughput,
    
    /// <summary>
    /// Optimized for environments with severe memory limitations.
    /// 
    /// This preset is like having a "resource-conscious manager" who operates
    /// within strict constraints, making hard choices to ensure the system
    /// stays within its limits.
    /// 
    /// Strategy: Sample with very small buffers
    /// Trade-offs: Minimal memory usage, statistical data preservation, reduced completeness
    /// 
    /// Configuration highlights:
    /// - Uses Sample strategy for bounded memory
    /// - Very small buffer sizes (1,000 items)
    /// - Very early intervention (50% watermark)
    /// - Samples every 2nd item under pressure
    /// - Minimal metrics overhead
    /// 
    /// Best for:
    /// - IoT devices with limited RAM
    /// - Edge computing scenarios
    /// - Embedded systems
    /// - Container environments with strict memory limits
    /// - Microservices in resource-constrained clusters
    /// </summary>
    MemoryConstrained,
    
    /// <summary>
    /// Optimized for real-time applications where freshness is critical.
    /// 
    /// This preset is like having a "real-time news director" who always wants
    /// the latest information and is willing to discard older news to make
    /// room for breaking stories.
    /// 
    /// Strategy: Drop with focus on latest data and quick response
    /// Trade-offs: Ultra-low latency, always fresh data, some data loss
    /// 
    /// Configuration highlights:
    /// - Uses Drop strategy for predictable latency
    /// - Small buffers for low latency (5,000 items)
    /// - Quick response to pressure (60% watermark)
    /// - Always prefer newer items
    /// - Fast measurement windows (10 seconds)
    /// 
    /// Best for:
    /// - Live video streaming
    /// - Real-time gaming
    /// - Stock price feeds
    /// - Live sports data
    /// - Interactive user interfaces
    /// </summary>
    RealTime,
    
    /// <summary>
    /// Optimized for analytics where statistical sampling is acceptable.
    /// 
    /// This preset is like having a "statistical analyst" who understands that
    /// you don't need every data point to get meaningful insights - a well-chosen
    /// sample can provide accurate results with much better resource efficiency.
    /// 
    /// Strategy: Adaptive sampling with intelligent metrics
    /// Trade-offs: Statistically valid results, efficient resource usage, controlled data reduction
    /// 
    /// Configuration highlights:
    /// - Uses Sample strategy with adaptive behavior
    /// - Large buffers for batch processing (25,000 items)
    /// - Intelligent sampling (10% under pressure)
    /// - Adaptive backpressure that learns from patterns
    /// - Comprehensive metrics for analysis
    /// 
    /// Best for:
    /// - Business intelligence systems
    /// - User behavior analytics
    /// - Performance monitoring
    /// - Market research data
    /// - Scientific data processing
    /// </summary>
    Analytics,
    
    /// <summary>
    /// Balanced configuration suitable for most general-purpose streaming scenarios.
    /// 
    /// This preset is like having a "sensible traffic manager" who applies
    /// proven strategies that work well in most situations. It provides
    /// good performance without extreme trade-offs in any direction.
    /// 
    /// Strategy: Buffer with reasonable limits and monitoring
    /// Trade-offs: Good balance of throughput, latency, and data completeness
    /// 
    /// Configuration highlights:
    /// - Uses Buffer strategy with reasonable limits
    /// - Standard buffer sizes (10,000 items)
    /// - Balanced watermark thresholds (80%/50%)
    /// - Comprehensive metrics collection
    /// - Suitable for most workloads
    /// 
    /// Best for:
    /// - General business applications
    /// - Standard data processing pipelines
    /// - Most web applications
    /// - Typical microservice scenarios
    /// - When you're unsure which strategy to choose
    /// </summary>
    Balanced
}