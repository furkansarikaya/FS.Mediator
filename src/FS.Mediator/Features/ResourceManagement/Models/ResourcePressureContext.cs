namespace FS.Mediator.Features.ResourceManagement.Models;

/// <summary>
/// Context information provided to custom cleanup actions.
/// This gives your custom logic access to current resource state and metrics.
/// </summary>
public class ResourcePressureContext
{
    /// <summary>
    /// Gets the current memory usage in bytes.
    /// </summary>
    public long CurrentMemoryUsage { get; init; }
    
    /// <summary>
    /// Gets the memory usage when monitoring started, for calculating growth.
    /// </summary>
    public long BaselineMemoryUsage { get; init; }
    
    /// <summary>
    /// Gets the rate of memory growth in bytes per second.
    /// </summary>
    public double MemoryGrowthRate { get; init; }
    
    /// <summary>
    /// Gets the number of garbage collections that have occurred.
    /// </summary>
    public int GarbageCollectionCount { get; init; }
    
    /// <summary>
    /// Gets the current request type being processed (null for background monitoring).
    /// </summary>
    public Type? CurrentRequestType { get; init; }
    
    /// <summary>
    /// Gets the timestamp when resource pressure was detected.
    /// </summary>
    public DateTime PressureDetectedAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets custom properties that can be set by the resource management behavior.
    /// </summary>
    public Dictionary<string, object> Properties { get; init; } = new();
}