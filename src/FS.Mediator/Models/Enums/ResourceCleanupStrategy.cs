namespace FS.Mediator.Models.Enums;

/// <summary>
/// Defines different strategies for cleaning up resources when thresholds are exceeded.
/// Each strategy represents a different balance between aggressiveness and performance impact.
/// 
/// Think of these strategies like different levels of "spring cleaning" in your home:
/// - Conservative: Light tidying up, minimal disruption
/// - Balanced: Thorough cleaning with manageable effort
/// - Aggressive: Deep cleaning that takes time but maximizes results
/// </summary>
public enum ResourceCleanupStrategy
{
    /// <summary>
    /// Minimal cleanup actions that are safe and have low performance impact.
    /// 
    /// Actions include:
    /// - Suggest garbage collection (GC.Collect() if AutoTriggerGarbageCollection is true)
    /// - Clear weak references
    /// - Log resource pressure warnings
    /// 
    /// Best for: Production systems where stability is paramount
    /// Trade-offs: Lowest performance impact but least memory reclamation
    /// </summary>
    Conservative,
    
    /// <summary>
    /// Balanced approach with moderate cleanup actions.
    /// 
    /// Actions include:
    /// - All Conservative actions
    /// - Force garbage collection for generations 0 and 1
    /// - Trim working set if possible
    /// - Clear finalizer queue
    /// 
    /// Best for: Most applications where some performance impact is acceptable
    /// Trade-offs: Good balance between memory reclamation and performance
    /// </summary>
    Balanced,
    
    /// <summary>
    /// Aggressive cleanup actions that prioritize memory reclamation over performance.
    /// 
    /// Actions include:
    /// - All Balanced actions
    /// - Full garbage collection (all generations)
    /// - Compact large object heap
    /// - Force disposal of tracked resources
    /// - Clear thread-local storage
    /// 
    /// Best for: Memory-constrained environments or batch processing scenarios
    /// Trade-offs: Maximum memory reclamation but highest performance impact
    /// </summary>
    Aggressive
}