namespace FS.Mediator.Features.StreamHandling.Models;

/// <summary>
/// Metrics tracking class for streaming operations.
/// This class encapsulates all the performance and health metrics we track during streaming.
/// </summary>
public class StreamProcessingMetrics
{
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public long ItemCount { get; set; }
    public bool CompletedSuccessfully { get; set; }
    public Exception? LastError { get; set; }
    public string RequestName { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    
    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;
}