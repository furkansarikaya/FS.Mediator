using FS.Mediator.Models.Enums;
using FS.Mediator.Models.Options;

namespace FS.Mediator.Models.Streaming;

/// <summary>
/// Manages circuit breaker state specifically for streaming operations.
/// This is similar to the regular CircuitBreakerState but adapted for streaming semantics.
/// </summary>
public class StreamingCircuitBreakerState(StreamingCircuitBreakerOptions options)
{
    private readonly object _stateLock = new();
    private readonly Queue<StreamingOperationResult> _streamHistory = new();
    
    private CircuitState _currentState = CircuitState.Closed;
    private DateTime _lastStateChangeTime = DateTime.UtcNow;
    private int _halfOpenStreamCount = 0;

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

    public bool ShouldAllowStream()
    {
        lock (_stateLock)
        {
            CleanupOldStreams();
            
            switch (_currentState)
            {
                case CircuitState.Closed:
                    return true;
                
                case CircuitState.Open:
                    if (DateTime.UtcNow - _lastStateChangeTime < options.DurationOfBreak) return false;
                    TransitionToHalfOpen();
                    return true;

                case CircuitState.HalfOpen:
                    if (_halfOpenStreamCount >= options.TrialRequestCount) return false;
                    _halfOpenStreamCount++;
                    return true;

                default:
                    return true;
            }
        }
    }

    public void RecordStreamResult(bool success, StreamingOperationMetrics metrics, Exception? exception = null)
    {
        lock (_stateLock)
        {
            var shouldCount = exception == null || options.ShouldCountAsFailure(exception);
            if (!shouldCount) return;

            // For streaming, consider partial success
            var effectiveSuccess = success || !success && options.PartialSuccessThreshold > 0 && metrics.ItemCount >= options.PartialSuccessThreshold;

            var result = new StreamingOperationResult
            {
                Success = effectiveSuccess,
                Timestamp = DateTime.UtcNow,
                ItemCount = metrics.ItemCount,
                Duration = metrics.Duration ?? TimeSpan.Zero
            };
            
            _streamHistory.Enqueue(result);
            CleanupOldStreams();

            switch (_currentState)
            {
                case CircuitState.Closed:
                    if (ShouldTripCircuit())
                    {
                        TransitionToOpen();
                    }
                    break;
                
                case CircuitState.HalfOpen:
                    if (effectiveSuccess)
                    {
                        if (_halfOpenStreamCount >= options.TrialRequestCount)
                        {
                            TransitionToClosed();
                        }
                    }
                    else
                    {
                        TransitionToOpen();
                    }
                    break;
            }
        }
    }

    private bool ShouldTripCircuit()
    {
        var recentStreams = _streamHistory.ToArray();
        
        if (recentStreams.Length < options.MinimumThroughput)
        {
            return false;
        }

        var failureCount = recentStreams.Count(s => !s.Success);
        var failurePercentage = (double)failureCount / recentStreams.Length * 100;

        return failurePercentage >= options.FailureThresholdPercentage;
    }

    private void CleanupOldStreams()
    {
        var cutoff = DateTime.UtcNow - options.SamplingDuration;
        
        while (_streamHistory.Count > 0 && _streamHistory.Peek().Timestamp < cutoff)
        {
            _streamHistory.Dequeue();
        }
    }

    private void TransitionToOpen()
    {
        _currentState = CircuitState.Open;
        _lastStateChangeTime = DateTime.UtcNow;
        _halfOpenStreamCount = 0;
    }

    private void TransitionToHalfOpen()
    {
        _currentState = CircuitState.HalfOpen;
        _lastStateChangeTime = DateTime.UtcNow;
        _halfOpenStreamCount = 0;
    }

    private void TransitionToClosed()
    {
        _currentState = CircuitState.Closed;
        _lastStateChangeTime = DateTime.UtcNow;
        _halfOpenStreamCount = 0;
        _streamHistory.Clear(); // Start fresh
    }

    private class StreamingOperationResult
    {
        public bool Success { get; set; }
        public DateTime Timestamp { get; set; }
        public long ItemCount { get; set; }
        public TimeSpan Duration { get; set; }
    }
}