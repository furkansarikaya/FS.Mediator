using FS.Mediator.Models.Enums;
using FS.Mediator.Models.ResourceManagement;

namespace FS.Mediator.Models.Options;

/// <summary>
/// Configuration options for resource management behavior.
/// 
/// Resource management in distributed systems is like being a careful homeowner -
/// you need to monitor your resources (memory, connections, handles) and clean up
/// before things get out of control. This behavior helps prevent memory leaks,
/// resource exhaustion, and performance degradation.
/// 
/// Think of this as your "resource watchdog" that keeps an eye on what your
/// application is consuming and takes action when necessary.
/// </summary>
public class ResourceManagementOptions
{
    /// <summary>
    /// Gets or sets the maximum memory usage threshold in bytes before triggering cleanup.
    /// When memory usage exceeds this limit, the behavior will attempt various cleanup strategies.
    /// Default is 512MB.
    /// 
    /// Set this based on your application's memory constraints:
    /// - Containerized apps: 50-80% of container memory limit
    /// - Server applications: 1-2GB depending on available RAM
    /// - Desktop applications: 256-512MB for responsive UI
    /// </summary>
    public long MaxMemoryThresholdBytes { get; set; } = 512_000_000; // 512MB
    
    /// <summary>
    /// Gets or sets the memory growth rate threshold (bytes per second) that triggers warnings.
    /// This helps detect memory leaks by monitoring how quickly memory usage is growing.
    /// Default is 10MB/second.
    /// 
    /// Rapid memory growth often indicates:
    /// - Memory leaks in request handlers
    /// - Unbounded caching
    /// - Large object accumulation
    /// - Resource handles not being disposed
    /// </summary>
    public long MemoryGrowthRateThresholdBytesPerSecond { get; set; } = 10_000_000; // 10MB/s
    
    /// <summary>
    /// Gets or sets whether to automatically trigger garbage collection when memory pressure is detected.
    /// This can help reclaim memory but may impact performance due to GC pauses.
    /// Default is false - let the GC handle its own timing.
    /// 
    /// Enable this for:
    /// - Memory-constrained environments
    /// - Long-running batch operations
    /// - Applications with predictable memory patterns
    /// 
    /// Keep disabled for:
    /// - High-performance scenarios requiring consistent latency
    /// - Applications with unpredictable workloads
    /// </summary>
    public bool AutoTriggerGarbageCollection { get; set; } = false;
    
    /// <summary>
    /// Gets or sets whether to force a full garbage collection (all generations).
    /// This is more aggressive but can reclaim more memory at the cost of longer pauses.
    /// Only applies when AutoTriggerGarbageCollection is true.
    /// Default is false - use standard GC.
    /// </summary>
    public bool ForceFullGarbageCollection { get; set; } = false;
    
    /// <summary>
    /// Gets or sets the interval for monitoring resource usage (in seconds).
    /// More frequent monitoring provides better responsiveness but uses more CPU.
    /// Default is 30 seconds.
    /// 
    /// Adjust based on your needs:
    /// - Critical applications: 10-15 seconds
    /// - Standard applications: 30-60 seconds
    /// - Background services: 60-300 seconds
    /// </summary>
    public int MonitoringIntervalSeconds { get; set; } = 30;
    
    /// <summary>
    /// Gets or sets the cleanup strategy to use when resource thresholds are exceeded.
    /// Different strategies have different trade-offs between aggressiveness and performance impact.
    /// Default is Conservative.
    /// </summary>
    public ResourceCleanupStrategy CleanupStrategy { get; set; } = ResourceCleanupStrategy.Conservative;
    
    /// <summary>
    /// Gets or sets whether to collect detailed memory statistics for diagnostics.
    /// This provides valuable debugging information but adds overhead.
    /// Default is false - only enable for troubleshooting.
    /// 
    /// Detailed stats include:
    /// - Generation-specific GC counts
    /// - Large object heap usage
    /// - Working set vs. managed memory
    /// - Memory pressure indicators
    /// </summary>
    public bool CollectDetailedMemoryStats { get; set; } = false;
    
    /// <summary>
    /// Gets or sets the maximum number of concurrent disposable resources to track.
    /// The behavior can track IDisposable resources and ensure they're properly cleaned up.
    /// Default is 1000 resources.
    /// 
    /// Higher values provide better tracking but use more memory.
    /// Set to 0 to disable resource tracking.
    /// </summary>
    public int MaxTrackedDisposableResources { get; set; } = 1000;
    
    /// <summary>
    /// Gets or sets whether to track and clean up abandoned disposable resources.
    /// This helps prevent resource leaks from handlers that forget to dispose resources.
    /// Default is true.
    /// 
    /// When enabled, the behavior will:
    /// - Track IDisposable objects created during request processing
    /// - Automatically dispose them if not disposed by the handler
    /// - Log warnings about undisposed resources
    /// </summary>
    public bool EnableDisposableResourceTracking { get; set; } = true;
    
    /// <summary>
    /// Gets or sets a custom action to execute when resource pressure is detected.
    /// This allows you to implement custom cleanup logic beyond the standard strategies.
    /// 
    /// Example uses:
    /// - Clear application-specific caches
    /// - Close idle database connections
    /// - Flush buffers to disk
    /// - Notify monitoring systems
    /// </summary>
    public Action<ResourcePressureContext>? CustomCleanupAction { get; set; }
}