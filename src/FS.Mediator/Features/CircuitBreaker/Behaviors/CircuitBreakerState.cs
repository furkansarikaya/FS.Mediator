using System.Collections.Concurrent;
using FS.Mediator.Features.CircuitBreaker.Models.Enums;
using FS.Mediator.Features.CircuitBreaker.Models.Options;

namespace FS.Mediator.Features.CircuitBreaker.Behaviors;

/// <summary>
/// Manages the state and statistics for a circuit breaker.
/// This class is thread-safe and handles the complex logic of state transitions
/// and failure tracking across concurrent requests.
/// </summary>
public class CircuitBreakerState
{
    private readonly CircuitBreakerOptions _options;
    private readonly object _stateLock = new();
    private readonly ConcurrentQueue<RequestResult> _requestHistory = new();
    
    private CircuitState _currentState = CircuitState.Closed;
    private DateTime _lastStateChangeTime = DateTime.UtcNow;
    private int _halfOpenRequestCount = 0;

    /// <summary>
    /// Initializes a new circuit breaker state with the specified options.
    /// </summary>
    /// <param name="options">Configuration options for the circuit breaker</param>
    public CircuitBreakerState(CircuitBreakerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Gets the current state of the circuit breaker.
    /// </summary>
    public CircuitState CurrentState
    {
        get
        {
            lock (_stateLock)
            {
                return _currentState;
            }
        }
    }

    /// <summary>
    /// Determines whether a request should be allowed through the circuit breaker.
    /// This is the main decision point that implements the circuit breaker logic.
    /// </summary>
    /// <returns>True if the request should be allowed, false if it should be rejected immediately</returns>
    public bool ShouldAllowRequest()
    {
        lock (_stateLock)
        {
            CleanupOldRequests();
            
            switch (_currentState)
            {
                case CircuitState.Closed:
                    return true; // Allow all requests in closed state
                
                case CircuitState.Open:
                    // Check if it's time to try half-open
                    if (DateTime.UtcNow - _lastStateChangeTime >= _options.DurationOfBreak)
                    {
                        TransitionToHalfOpen();
                        return true; // Allow the first trial request
                    }
                    return false; // Reject request while circuit is open
                
                case CircuitState.HalfOpen:
                    // Allow limited number of trial requests
                    if (_halfOpenRequestCount >= _options.TrialRequestCount) return false; // Too many trial requests already in flight
                    _halfOpenRequestCount++;
                    return true;

                default:
                    return true;
            }
        }
    }

    /// <summary>
    /// Records the result of a request execution.
    /// This method updates the circuit breaker's internal state based on success or failure.
    /// </summary>
    /// <param name="success">Whether the request succeeded</param>
    /// <param name="exception">The exception if the request failed, null if successful</param>
    public void RecordResult(bool success, Exception? exception = null)
    {
        lock (_stateLock)
        {
            var shouldCount = exception == null || _options.ShouldCountAsFailure(exception);
            if (!shouldCount) return; // Don't record exceptions we're configured to ignore

            var result = new RequestResult
            {
                Success = success,
                Timestamp = DateTime.UtcNow
            };
            
            _requestHistory.Enqueue(result);
            CleanupOldRequests();

            switch (_currentState)
            {
                case CircuitState.Closed:
                    if (ShouldTripCircuit())
                    {
                        TransitionToOpen();
                    }
                    break;
                
                case CircuitState.HalfOpen:
                    if (success)
                    {
                        // If we've completed all trial requests successfully, close the circuit
                        if (_halfOpenRequestCount >= _options.TrialRequestCount)
                        {
                            TransitionToClosed();
                        }
                    }
                    else
                    {
                        // Any failure in half-open state immediately opens the circuit
                        TransitionToOpen();
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Determines if the circuit should trip (open) based on current failure statistics.
    /// This implements the mathematical logic for failure threshold detection.
    /// </summary>
    private bool ShouldTripCircuit()
    {
        var recentRequests = _requestHistory.ToArray();
        
        if (recentRequests.Length < _options.MinimumThroughput)
        {
            return false; // Not enough data to make a decision
        }

        var failureCount = recentRequests.Count(r => !r.Success);
        var failurePercentage = (double)failureCount / recentRequests.Length * 100;

        return failurePercentage >= _options.FailureThresholdPercentage;
    }

    /// <summary>
    /// Removes old request records that fall outside the sampling window.
    /// This maintains a rolling window of statistics for decision making.
    /// </summary>
    private void CleanupOldRequests()
    {
        var cutoff = DateTime.UtcNow - _options.SamplingDuration;
        
        while (_requestHistory.TryPeek(out var result) && result.Timestamp < cutoff)
        {
            _requestHistory.TryDequeue(out _);
        }
    }

    private void TransitionToOpen()
    {
        _currentState = CircuitState.Open;
        _lastStateChangeTime = DateTime.UtcNow;
        _halfOpenRequestCount = 0;
    }

    private void TransitionToHalfOpen()
    {
        _currentState = CircuitState.HalfOpen;
        _lastStateChangeTime = DateTime.UtcNow;
        _halfOpenRequestCount = 0;
    }

    private void TransitionToClosed()
    {
        _currentState = CircuitState.Closed;
        _lastStateChangeTime = DateTime.UtcNow;
        _halfOpenRequestCount = 0;
        // Clear history on successful recovery to start fresh
        while (_requestHistory.TryDequeue(out _)) { }
    }

    /// <summary>
    /// Represents the result of a single request for statistical tracking.
    /// </summary>
    private class RequestResult
    {
        public bool Success { get; set; }
        public DateTime Timestamp { get; set; }
    }
}