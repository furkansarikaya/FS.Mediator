namespace FS.Mediator.Models.Options;

/// <summary>
/// Configuration options for circuit breaker behavior.
/// Circuit breaker protects your system from cascade failures by temporarily stopping
/// requests to failing services, giving them time to recover while preventing resource exhaustion.
/// </summary>
public class CircuitBreakerOptions
{
    /// <summary>
    /// Gets or sets the failure threshold percentage that triggers the circuit to open.
    /// For example, if set to 50, the circuit opens when 50% or more requests fail
    /// within the sampling window. Default is 50%.
    /// Think of this as your "pain tolerance" - how much failure you can accept before protection kicks in.
    /// </summary>
    public double FailureThresholdPercentage { get; set; } = 50.0;
    
    /// <summary>
    /// Gets or sets the minimum number of requests required in the sampling window before
    /// the circuit breaker can open. This prevents the circuit from opening due to a small
    /// number of failures when traffic is low. Default is 5 requests.
    /// This is your "statistical significance" threshold.
    /// </summary>
    public int MinimumThroughput { get; set; } = 5;
    
    /// <summary>
    /// Gets or sets the time window for collecting failure statistics.
    /// The circuit breaker tracks failures within this rolling window to determine
    /// if the failure threshold has been exceeded. Default is 60 seconds.
    /// Think of this as your "memory span" - how far back you look when making decisions.
    /// </summary>
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(60);
    
    /// <summary>
    /// Gets or sets how long the circuit stays open before transitioning to half-open state.
    /// During this time, all requests fail immediately without hitting the underlying service.
    /// This gives the failing service time to recover. Default is 30 seconds.
    /// This is your "recovery grace period".
    /// </summary>
    public TimeSpan DurationOfBreak { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Gets or sets the number of trial requests allowed in the half-open state.
    /// When the circuit is half-open, only this many requests are allowed through
    /// to test if the service has recovered. Default is 3 requests.
    /// This is your "recovery test size".
    /// </summary>
    public int TrialRequestCount { get; set; } = 3;
    
    /// <summary>
    /// Gets or sets a function to determine if an exception should be counted as a failure.
    /// By default, all exceptions are considered failures, but you can customize this
    /// to ignore certain types of exceptions (like validation errors) that don't indicate
    /// service health issues.
    /// </summary>
    public Func<Exception, bool> ShouldCountAsFailure { get; set; } = _ => true;
}