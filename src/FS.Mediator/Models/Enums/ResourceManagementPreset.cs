namespace FS.Mediator.Models.Enums;

/// <summary>
/// Predefined resource management configurations optimized for common deployment scenarios.
/// 
/// These presets represent different "resource management philosophies" based on your
/// deployment environment and performance requirements. Think of them as choosing
/// the right tool for the right job.
/// </summary>
public enum ResourceManagementPreset
{
    /// <summary>
    /// Optimized for memory-constrained environments like containers and embedded systems.
    /// 
    /// This preset is like being a "strict budgeter" with your memory - it monitors
    /// closely, acts quickly, and prioritizes memory conservation over performance.
    /// 
    /// Configuration highlights:
    /// - Low memory thresholds (256MB)
    /// - Aggressive cleanup strategies
    /// - Frequent monitoring
    /// - Automatic garbage collection
    /// - Full resource tracking
    /// 
    /// Best for:
    /// - Docker containers with memory limits
    /// - Kubernetes pods with resource constraints
    /// - Embedded systems
    /// - Shared hosting environments
    /// - Cost-optimized cloud deployments
    /// </summary>
    MemoryConstrained,
    
    /// <summary>
    /// Optimized for high-performance applications where latency is critical.
    /// 
    /// This preset is like being a "performance athlete" - it prioritizes speed
    /// and consistency over aggressive resource management, trusting that adequate
    /// resources are available.
    /// 
    /// Configuration highlights:
    /// - High memory thresholds (1GB+)
    /// - Conservative cleanup strategies
    /// - Infrequent monitoring to reduce overhead
    /// - Let GC manage itself
    /// - Minimal resource tracking overhead
    /// 
    /// Best for:
    /// - High-frequency trading systems
    /// - Real-time gaming applications
    /// - Low-latency APIs
    /// - Performance-critical microservices
    /// - Applications with strict SLA requirements
    /// </summary>
    HighPerformance,
    
    /// <summary>
    /// Balanced configuration suitable for most production applications.
    /// 
    /// This preset is like being a "sensible homeowner" - it maintains good
    /// practices without being excessive, providing reliable operation for
    /// typical business applications.
    /// 
    /// Configuration highlights:
    /// - Moderate memory thresholds (512MB)
    /// - Balanced cleanup strategies
    /// - Regular monitoring intervals
    /// - Selective garbage collection
    /// - Standard resource tracking
    /// 
    /// Best for:
    /// - Web applications
    /// - Business APIs
    /// - Standard microservices
    /// - Most enterprise applications
    /// - General-purpose services
    /// </summary>
    Balanced,
    
    /// <summary>
    /// Optimized for development and debugging scenarios.
    /// 
    /// This preset is like having "developer-friendly training wheels" - it
    /// provides extensive monitoring and diagnostics while being forgiving
    /// about resource usage to help identify and fix issues.
    /// 
    /// Configuration highlights:
    /// - Very high memory thresholds (2GB+)
    /// - Conservative cleanup to avoid masking issues
    /// - Detailed monitoring and metrics
    /// - Comprehensive resource tracking
    /// - Predictable behavior for debugging
    /// 
    /// Best for:
    /// - Local development environments
    /// - Testing and QA environments
    /// - Debugging production issues
    /// - Performance profiling
    /// - Learning and experimentation
    /// </summary>
    Development
}