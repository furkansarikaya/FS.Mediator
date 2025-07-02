namespace FS.Mediator.Features.Retry.Models.Enums;

/// <summary>
/// Defines the different retry timing strategies available.
/// Each strategy represents a different mathematical approach to spacing retry attempts.
/// </summary>
public enum RetryStrategy
{
    /// <summary>
    /// Wait the same amount of time between each retry attempt.
    /// Simple and predictable: delay, delay, delay...
    /// Best for: Simple scenarios where you want consistent timing
    /// </summary>
    FixedDelay,
    
    /// <summary>
    /// Double the wait time after each failed attempt.
    /// Mathematical progression: 1s, 2s, 4s, 8s...
    /// Best for: APIs with rate limiting, overloaded databases
    /// </summary>
    ExponentialBackoff,
    
    /// <summary>
    /// Exponential backoff plus random variation to prevent thundering herd.
    /// Adds randomness: 1s±jitter, 2s±jitter, 4s±jitter...
    /// Best for: High-concurrency scenarios with many clients
    /// </summary>
    ExponentialBackoffWithJitter
}