using FS.Mediator.Models.Enums;

namespace FS.Mediator.Models.Backpressure;

/// <summary>
/// Context information provided when backpressure is triggered.
/// This gives custom handlers access to current state and metrics.
/// </summary>
public class BackpressureContext
{
    /// <summary>
    /// Gets the strategy being applied to handle backpressure.
    /// </summary>
    public BackpressureStrategy Strategy { get; init; }
    
    /// <summary>
    /// Gets the current metrics that triggered backpressure.
    /// </summary>
    public BackpressureMetrics Metrics { get; init; } = new();
    
    /// <summary>
    /// Gets the timestamp when backpressure was triggered.
    /// </summary>
    public DateTime TriggeredAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets the type of request being processed when backpressure occurred.
    /// </summary>
    public Type? RequestType { get; init; }
    
    /// <summary>
    /// Gets custom properties that can be set by the backpressure behavior.
    /// </summary>
    public Dictionary<string, object> Properties { get; init; } = new();
}
