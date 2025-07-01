namespace FS.Mediator.Models.Enums;

/// <summary>
/// Represents the current state of a circuit breaker.
/// This enum embodies the finite state machine that protects your services.
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// Normal operation state. Requests flow through normally, and failures are monitored.
    /// The circuit remains closed as long as the failure rate stays below the threshold.
    /// </summary>
    Closed,
    
    /// <summary>
    /// Protection state. The circuit is open and requests fail immediately without
    /// hitting the underlying service. This protects both the failing service and your application.
    /// </summary>
    Open,
    
    /// <summary>
    /// Testing state. A limited number of requests are allowed through to test
    /// if the service has recovered. Based on these test results, the circuit
    /// either closes (recovery confirmed) or opens again (still failing).
    /// </summary>
    HalfOpen
}