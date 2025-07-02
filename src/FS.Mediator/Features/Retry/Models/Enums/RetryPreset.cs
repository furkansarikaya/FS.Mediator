namespace FS.Mediator.Features.Retry.Models.Enums;

/// <summary>
/// Predefined retry policy configurations for common scenarios.
/// These presets represent battle-tested configurations that work well
/// for typical distributed system challenges.
/// </summary>
public enum RetryPreset
{
    /// <summary>
    /// Conservative retry policy with minimal attempts and fixed delays.
    /// Best for: Operations where speed is more important than resilience,
    /// or when you want to fail fast to avoid user frustration.
    /// Configuration: 2 retries, 500ms fixed delay, 10s total timeout
    /// </summary>
    Conservative,
    
    /// <summary>
    /// Aggressive retry policy with more attempts and intelligent backoff.
    /// Best for: Critical operations that must succeed, background processes,
    /// or when user experience can tolerate longer wait times.
    /// Configuration: 5 retries, exponential backoff with jitter, 2min total timeout
    /// </summary>
    Aggressive,
    
    /// <summary>
    /// Optimized for database operations and connection issues.
    /// Best for: Entity Framework operations, direct database calls,
    /// repository pattern implementations.
    /// Configuration: 3 retries, exponential backoff, database-specific exception handling
    /// </summary>
    Database,
    
    /// <summary>
    /// Optimized for HTTP API calls and network operations.
    /// Best for: External API integrations, microservice communication,
    /// third-party service calls.
    /// Configuration: 4 retries, jittered exponential backoff, HTTP-specific exception handling
    /// </summary>
    HttpApi
}