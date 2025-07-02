using System.Collections.Concurrent;
using FS.Mediator.Features.CircuitBreaker.Exceptions;
using FS.Mediator.Features.CircuitBreaker.Models.Enums;
using FS.Mediator.Features.CircuitBreaker.Models.Options;
using FS.Mediator.Features.RequestHandling.Core;
using Microsoft.Extensions.Logging;

namespace FS.Mediator.Features.CircuitBreaker.Behaviors;

/// <summary>
/// Pipeline behavior that implements circuit breaker pattern for protecting against cascade failures.
/// 
/// The circuit breaker monitors the failure rate of requests and automatically opens
/// when failures exceed the configured threshold, preventing further requests from
/// hitting the failing service and giving it time to recover.
/// 
/// This behavior is particularly valuable in microservice architectures where
/// one failing service can bring down the entire system if not properly isolated.
/// </summary>
/// <typeparam name="TRequest">The type of request being processed.</typeparam>
/// <typeparam name="TResponse">The type of response returned by the request.</typeparam>
public class CircuitBreakerBehavior<TRequest, TResponse>(
    ILogger<CircuitBreakerBehavior<TRequest, TResponse>> logger,
    CircuitBreakerOptions options) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<CircuitBreakerBehavior<TRequest, TResponse>> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly CircuitBreakerOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    
    // Static concurrent dictionary to maintain circuit breaker state per request type
    // This ensures that different request types have independent circuit breakers
    private static readonly ConcurrentDictionary<Type, CircuitBreakerState> CircuitStates = new();

    /// <inheritdoc />
    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
    {
        var requestType = typeof(TRequest);
        var requestName = requestType.Name;
        
        // Get or create circuit breaker state for this request type
        var circuitState = CircuitStates.GetOrAdd(requestType, _ => new CircuitBreakerState(_options));
        
        // Check if the circuit breaker allows this request
        if (!circuitState.ShouldAllowRequest())
        {
            _logger.LogWarning("Circuit breaker is open for {RequestName}. Request rejected to prevent cascade failure", requestName);
            throw new CircuitBreakerOpenException(requestType);
        }

        var currentState = circuitState.CurrentState;
        
        try
        {
            // Log when we're in testing mode (half-open state)
            if (currentState == CircuitState.HalfOpen)
            {
                _logger.LogInformation("Circuit breaker for {RequestName} is half-open. Testing service recovery with trial request", requestName);
            }

            // Execute the request
            var result = await next(cancellationToken);
            
            // Record successful execution
            circuitState.RecordResult(true);
            
            // Log successful recovery if we were testing
            if (currentState == CircuitState.HalfOpen)
            {
                _logger.LogInformation("Trial request for {RequestName} succeeded. Circuit breaker may transition to closed state", requestName);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            // Record the failure
            circuitState.RecordResult(false, ex);
            
            // Log circuit state changes
            var newState = circuitState.CurrentState;
            if (currentState != newState)
            {
                _logger.LogWarning("Circuit breaker for {RequestName} transitioned from {OldState} to {NewState} due to exception: {Exception}", 
                    requestName, currentState, newState, ex.Message);
            }
            else if (currentState == CircuitState.HalfOpen)
            {
                _logger.LogWarning("Trial request for {RequestName} failed. Circuit breaker opened again: {Exception}", 
                    requestName, ex.Message);
            }
            
            // Re-throw the original exception
            throw;
        }
    }
}