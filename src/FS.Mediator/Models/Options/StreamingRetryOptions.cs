using FS.Mediator.Models.Enums;

namespace FS.Mediator.Models.Options;

/// <summary>
/// Configuration options for streaming retry behavior.
/// 
/// Retry logic for streams is fundamentally different from regular requests because:
/// 1. Streams can partially succeed (yield some items before failing)
/// 2. Restarting an entire stream might be expensive or inappropriate
/// 3. State management becomes crucial (where do we resume from?)
/// 
/// These options help you configure retry behavior that makes sense for your specific streaming scenarios.
/// </summary>
public class StreamingRetryOptions
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts for a failed stream.
    /// Each retry attempt will restart the entire stream from the beginning.
    /// Default is 2 attempts (3 total including initial attempt).
    /// 
    /// Consider the cost of retrying: if your stream processes a million database records,
    /// retrying might be expensive. For such cases, you might prefer lower retry counts
    /// with better error handling in your stream handler.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 2;
    
    /// <summary>
    /// Gets or sets the initial delay before the first retry attempt.
    /// This delay helps avoid immediately retrying a failing operation,
    /// giving downstream services time to recover.
    /// Default is 2 seconds.
    /// 
    /// For database streams, consider longer delays (5-10 seconds) to allow
    /// database connection pools to recover. For in-memory operations,
    /// shorter delays (500ms-1s) might be appropriate.
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(2);
    
    /// <summary>
    /// Gets or sets the retry strategy for calculating delays between attempts.
    /// Different strategies work better for different failure scenarios:
    /// 
    /// - FixedDelay: Same delay between each retry (good for predictable failures)
    /// - ExponentialBackoff: Increasing delays (good for overloaded systems)
    /// - ExponentialBackoffWithJitter: Randomized increasing delays (best for high-concurrency)
    /// 
    /// Default is ExponentialBackoff, which works well for most streaming scenarios.
    /// </summary>
    public RetryStrategy RetryStrategy { get; set; } = RetryStrategy.ExponentialBackoff;
    
    /// <summary>
    /// Gets or sets the maximum total time to spend on retries for a single stream.
    /// Even if retry attempts remain, the operation will stop if this timeout is exceeded.
    /// Default is 5 minutes.
    /// 
    /// This is crucial for streaming operations because they can run for long periods.
    /// Set this based on your business requirements: batch processing might tolerate
    /// longer timeouts (30-60 minutes), while user-facing streams should be shorter (1-5 minutes).
    /// </summary>
    public TimeSpan MaxTotalRetryTime { get; set; } = TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// Gets or sets a function to determine if an exception should trigger a retry.
    /// This is your "retry decision engine" that determines which exceptions are
    /// worth retrying and which should fail immediately.
    /// 
    /// Default behavior retries common transient exceptions like network timeouts
    /// and database connection issues, but not business logic exceptions.
    /// </summary>
    public Func<Exception, bool> ShouldRetryPredicate { get; set; } = DefaultShouldRetryPredicate;
    
    /// <summary>
    /// Gets or sets the retry strategy for resuming streams.
    /// 
    /// - RestartFromBeginning: Always restart the entire stream (safest, but potentially expensive)
    /// - ResumeFromLastPosition: Try to resume from where the stream failed (efficient, but requires handler support)
    /// 
    /// Default is RestartFromBeginning because it's the safest option.
    /// ResumeFromLastPosition requires your stream handlers to support positioning/seeking.
    /// </summary>
    public StreamingRetryStrategy ResumeStrategy { get; set; } = StreamingRetryStrategy.RestartFromBeginning;
    
    private static bool DefaultShouldRetryPredicate(Exception exception)
    {
        // Network and connectivity issues - almost always worth retrying
        if (exception is System.Net.Http.HttpRequestException or 
            System.Net.Sockets.SocketException or 
            TaskCanceledException or 
            OperationCanceledException)
        {
            return true;
        }

        // Database-related transient issues
        if (exception.GetType().Name.Contains("Timeout") ||
            exception.GetType().Name.Contains("Connection") ||
            exception.GetType().Name.Contains("Deadlock") ||
            exception.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Don't retry business logic exceptions, validation errors, or argument exceptions
        // These are likely permanent failures that won't be fixed by retrying
        return false;
    }
}