namespace FS.Mediator.Features.StreamHandling.Exceptions;

/// <summary>
/// Specialized exception that provides detailed information about streaming operation failures.
/// 
/// Streaming operations can fail in unique ways:
/// - They might succeed partially (yield some items before failing)
/// - They might fail on specific items while others succeed
/// - They might fail due to downstream service issues after producing results
/// 
/// This exception captures these nuances to help with debugging and error handling.
/// </summary>
public class StreamingOperationException : Exception
{
    /// <summary>
    /// Initializes a new streaming operation exception.
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="streamPosition">The position in the stream where the error occurred (-1 if unknown)</param>
    /// <param name="innerException">The underlying exception that caused the stream to fail</param>
    public StreamingOperationException(string message, long streamPosition = -1, Exception? innerException = null) 
        : base(message, innerException)
    {
        StreamPosition = streamPosition;
        FailureTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the position in the stream where the failure occurred.
    /// -1 indicates the position is unknown or the failure happened before streaming started.
    /// This is valuable for retry logic - you might want to retry from this position.
    /// </summary>
    public long StreamPosition { get; }
    
    /// <summary>
    /// Gets the timestamp when the streaming operation failed.
    /// Useful for debugging and understanding failure patterns over time.
    /// </summary>
    public DateTime FailureTime { get; }
    
    /// <summary>
    /// Gets or sets the number of items that were successfully yielded before the failure.
    /// This helps determine if the stream was partially successful.
    /// </summary>
    public long SuccessfulItemCount { get; set; }
}