namespace FS.Mediator.Exceptions;

/// <summary>
/// Circuit breaker exception thrown when requests are rejected due to an open circuit.
/// This exception indicates that the circuit breaker is protecting the system
/// by failing fast rather than attempting to call a failing service.
/// </summary>
public class CircuitBreakerOpenException : Exception
{
    /// <summary>
    /// Initializes a new instance of the CircuitBreakerOpenException.
    /// </summary>
    /// <param name="requestType">The type of request that was rejected</param>
    public CircuitBreakerOpenException(Type requestType) 
        : base($"Circuit breaker is open for request type '{requestType.Name}'. Request rejected to prevent cascade failure.")
    {
        RequestType = requestType;
    }

    /// <summary>
    /// Gets the type of request that was rejected by the circuit breaker.
    /// </summary>
    public Type RequestType { get; }
}
