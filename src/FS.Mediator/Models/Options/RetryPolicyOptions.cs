using FS.Mediator.Models.Enums;

namespace FS.Mediator.Models.Options;

/// <summary>
/// Configuration options for retry policy behavior.
/// This class encapsulates all the parameters needed to configure intelligent retry logic
/// for handling transient failures in distributed systems.
/// </summary>
public class RetryPolicyOptions
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// Think of this as your "persistence level" - how many times you're willing
    /// to try before giving up. Default is 3 attempts.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
    
    /// <summary>
    /// Gets or sets the initial delay between retry attempts.
    /// This serves as the base timing for your retry strategy. Default is 1 second.
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
    
    /// <summary>
    /// Gets or sets the retry strategy to use.
    /// Different strategies work better for different scenarios:
    /// - FixedDelay: Good for consistent, predictable delays
    /// - ExponentialBackoff: Best for rate-limited APIs and overloaded systems
    /// - ExponentialBackoffWithJitter: Optimal for high-concurrency scenarios
    /// </summary>
    public RetryStrategy Strategy { get; set; } = RetryStrategy.ExponentialBackoff;
    
    /// <summary>
    /// Gets or sets the maximum total time to spend on retries.
    /// This acts as a circuit breaker - even if you haven't used all retry attempts,
    /// you'll stop if this timeout is exceeded. Default is 30 seconds.
    /// </summary>
    public TimeSpan MaxTotalRetryTime { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Gets or sets a function to determine if an exception should trigger a retry.
    /// This is your "retry decision engine" - you can customize which exceptions
    /// are worth retrying and which should fail immediately.
    /// By default, retries most common transient exceptions.
    /// </summary>
    public Func<Exception, bool> ShouldRetryPredicate { get; set; } = DefaultShouldRetryPredicate;
    
    /// <summary>
    /// Default predicate that determines whether an exception warrants a retry attempt.
    /// This method embodies years of distributed systems wisdom about which failures
    /// are typically transient vs. permanent.
    /// </summary>
    /// <param name="exception">The exception to evaluate</param>
    /// <returns>True if the exception type suggests a retry might succeed</returns>
    private static bool DefaultShouldRetryPredicate(Exception exception)
    {
        // Network-related exceptions - these are almost always transient
        if (exception is System.Net.Http.HttpRequestException or System.Net.Sockets.SocketException or TaskCanceledException or OperationCanceledException)
        {
            return true;
        }

        // Database-related transient exceptions
        return exception.GetType().Name.Contains("Timeout") ||
               exception.GetType().Name.Contains("Connection") ||
               exception.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase);
        // Generally, don't retry business logic exceptions or validation errors
        // These are likely permanent failures that won't be fixed by retrying
    }
}
