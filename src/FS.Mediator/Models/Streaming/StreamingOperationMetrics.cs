namespace FS.Mediator.Models.Streaming;

/// <summary>
/// Provides metadata about a streaming operation for monitoring and debugging.
/// This class captures important metrics that help you understand the performance
/// and health of your streaming operations.
/// </summary>
public class StreamingOperationMetrics
{
    /// <summary>
    /// Gets or sets when the streaming operation started.
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets or sets when the streaming operation completed (successfully or with error).
    /// </summary>
    public DateTime? EndTime { get; set; }
    
    /// <summary>
    /// Gets the total duration of the streaming operation.
    /// Returns null if the operation hasn't completed yet.
    /// </summary>
    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;
    
    /// <summary>
    /// Gets or sets the total number of items yielded by the stream.
    /// This is incremented as items flow through the pipeline.
    /// </summary>
    public long ItemCount { get; set; }
    
    /// <summary>
    /// Gets or sets the number of errors that occurred during streaming.
    /// This includes both recoverable errors (handled by retry logic) and fatal errors.
    /// </summary>
    public int ErrorCount { get; set; }
    
    /// <summary>
    /// Gets or sets whether the streaming operation completed successfully.
    /// True means the stream reached its natural end without unhandled errors.
    /// </summary>
    public bool CompletedSuccessfully { get; set; }
    
    /// <summary>
    /// Gets or sets the last error that occurred during streaming.
    /// This is useful for debugging when operations fail.
    /// </summary>
    public Exception? LastError { get; set; }
    
    /// <summary>
    /// Gets the average items per second processed by the stream.
    /// This is a key performance indicator for streaming operations.
    /// Returns null if the operation hasn't completed or no items were processed.
    /// </summary>
    public double? ItemsPerSecond
    {
        get
        {
            if (!Duration.HasValue || Duration.Value.TotalSeconds == 0 || ItemCount == 0)
                return null;
            
            return ItemCount / Duration.Value.TotalSeconds;
        }
    }
}