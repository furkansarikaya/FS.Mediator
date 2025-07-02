namespace FS.Mediator.Features.CircuitBreaker.Models.Enums;

/// <summary>
/// Predefined circuit breaker configurations for common scenarios.
/// These presets represent different approaches to failure tolerance and recovery timing,
/// each optimized for specific types of services and failure patterns.
/// </summary>
public enum CircuitBreakerPreset
{
    /// <summary>
    /// Highly sensitive circuit breaker that trips quickly and recovers fast.
    /// Best for: Critical services where even small failure rates are unacceptable,
    /// user-facing operations where fast failure detection is important.
    /// Configuration: 30% failure threshold, 30s sampling, 15s break duration
    /// </summary>
    Sensitive,
    
    /// <summary>
    /// Balanced circuit breaker with moderate thresholds and timing.
    /// Best for: General-purpose services, internal APIs, most business operations.
    /// This is the recommended starting point for most applications.
    /// Configuration: 50% failure threshold, 60s sampling, 30s break duration
    /// </summary>
    Balanced,
    
    /// <summary>
    /// Resilient circuit breaker that tolerates higher failure rates before tripping.
    /// Best for: Non-critical services, batch operations, services with expected intermittent failures,
    /// background processing where availability is less critical than avoiding false positives.
    /// Configuration: 70% failure threshold, 2min sampling, 1min break duration
    /// </summary>
    Resilient,
    
    /// <summary>
    /// Optimized for database operations with conservative failure handling.
    /// Best for: Entity Framework operations, repository patterns, direct database calls.
    /// Excludes business logic exceptions from failure counting.
    /// Configuration: 40% failure threshold, 1min sampling, 45s break duration
    /// </summary>
    Database,
    
    /// <summary>
    /// Optimized for external API calls with tolerance for network variability.
    /// Best for: Third-party API integrations, microservice communication, external dependencies.
    /// Ignores client errors (4xx) and only counts server errors (5xx) and network issues.
    /// Configuration: 60% failure threshold, 3min sampling, 60s break duration
    /// </summary>
    ExternalApi
}