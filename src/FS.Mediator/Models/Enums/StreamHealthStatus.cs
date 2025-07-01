namespace FS.Mediator.Models.Enums;

/// <summary>
/// Represents the overall health status of a streaming operation.
/// This provides a simple "traffic light" system for quickly understanding stream health.
/// </summary>
public enum StreamHealthStatus
{
    /// <summary>
    /// Stream is operating normally with no significant issues detected.
    /// </summary>
    Healthy,
    
    /// <summary>
    /// Stream is functional but some concerning metrics have been detected.
    /// The stream can continue operating but should be monitored closely.
    /// </summary>
    Warning,
    
    /// <summary>
    /// Stream has significant issues that may impact its ability to function properly.
    /// Immediate attention may be required.
    /// </summary>
    Unhealthy,
    
    /// <summary>
    /// Stream has failed and is no longer processing items.
    /// </summary>
    Failed
}