namespace FS.Mediator.Models.Enums;

/// <summary>
/// Defines different strategies for resuming failed streams.
/// </summary>
public enum StreamingRetryStrategy
{
    /// <summary>
    /// Always restart the stream from the beginning.
    /// This is the safest option but can be expensive for large streams.
    /// Best for: Small to medium streams, or when data consistency is critical.
    /// </summary>
    RestartFromBeginning,
    
    /// <summary>
    /// Attempt to resume the stream from the last successfully processed position.
    /// This requires the stream handler to support positioning/seeking operations.
    /// Best for: Large streams where reprocessing is expensive, and when handlers support resumption.
    /// </summary>
    ResumeFromLastPosition
}
