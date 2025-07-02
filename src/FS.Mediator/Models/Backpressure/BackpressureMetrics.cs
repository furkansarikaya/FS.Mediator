namespace FS.Mediator.Models.Backpressure;

/// <summary>
/// Metrics used for making backpressure decisions.
/// This data helps the system understand current load and performance characteristics.
/// </summary>
public class BackpressureMetrics
{
    /// <summary>
    /// Current number of items in the buffer.
    /// </summary>
    public int CurrentBufferSize { get; init; }
    
    /// <summary>
    /// Maximum allowed buffer size.
    /// </summary>
    public int MaxBufferSize { get; init; }
    
    /// <summary>
    /// Rate at which items are being produced (items per second).
    /// </summary>
    public double ProducerRate { get; init; }
    
    /// <summary>
    /// Rate at which items are being consumed (items per second).
    /// </summary>
    public double ConsumerRate { get; init; }
    
    /// <summary>
    /// How long backpressure has been active (null if not currently active).
    /// </summary>
    public TimeSpan? BackpressureDuration { get; init; }
    
    /// <summary>
    /// Current memory usage in bytes.
    /// </summary>
    public long MemoryUsage { get; init; }
    
    /// <summary>
    /// Additional custom metrics that can be used for decision making.
    /// </summary>
    public Dictionary<string, object> CustomMetrics { get; init; } = new();
}