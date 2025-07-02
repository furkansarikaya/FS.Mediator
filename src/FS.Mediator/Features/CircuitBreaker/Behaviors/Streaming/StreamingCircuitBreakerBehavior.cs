using System.Runtime.CompilerServices;
using System.Threading.Channels;
using FS.Mediator.Features.CircuitBreaker.Models.Enums;
using FS.Mediator.Features.CircuitBreaker.Models.Options;
using FS.Mediator.Features.CircuitBreaker.Models.Streaming;
using FS.Mediator.Features.StreamHandling.Core;
using FS.Mediator.Features.StreamHandling.Exceptions;
using FS.Mediator.Features.StreamHandling.Models;
using Microsoft.Extensions.Logging;

namespace FS.Mediator.Features.CircuitBreaker.Behaviors.Streaming;

/// <summary>
/// Streaming pipeline behavior that implements circuit breaker pattern for stream operations.
/// 
/// Circuit breaker for streams protects against cascade failures by monitoring stream-level
/// failures rather than individual item failures. This implementation uses the channel pattern
/// to ensure circuit breaker logic (with try-catch) is completely separated from yielding.
/// </summary>
/// <typeparam name="TRequest">The type of streaming request</typeparam>
/// <typeparam name="TResponse">The type of each item in the stream</typeparam>
public class StreamingCircuitBreakerBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    private readonly ILogger<StreamingCircuitBreakerBehavior<TRequest, TResponse>> _logger;
    private readonly StreamingCircuitBreakerOptions _options;
    
    // Static state shared across all instances for this request type
    private static readonly Dictionary<Type, StreamingCircuitBreakerState> CircuitStates = new();
    private static readonly object StateLock = new();

    public StreamingCircuitBreakerBehavior(
        ILogger<StreamingCircuitBreakerBehavior<TRequest, TResponse>> logger,
        StreamingCircuitBreakerOptions options)
    {
        _logger = logger;
        _options = options;
    }

    public async IAsyncEnumerable<TResponse> HandleAsync(
        TRequest request, 
        StreamRequestHandlerDelegate<TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestType = typeof(TRequest);
        var requestName = requestType.Name;
        
        // Get or create circuit breaker state for this request type
        StreamingCircuitBreakerState circuitState;
        lock (StateLock)
        {
            if (!CircuitStates.TryGetValue(requestType, out circuitState))
            {
                circuitState = new StreamingCircuitBreakerState(_options);
                CircuitStates[requestType] = circuitState;
            }
        }

        // Check circuit breaker state before starting stream
        if (!circuitState.ShouldAllowStream())
        {
            _logger.LogWarning("Circuit breaker is open for {RequestName}. Stream rejected to prevent cascade failure", requestName);
            throw new StreamingOperationException($"Circuit breaker is open for {requestName}", 0);
        }

        var currentState = circuitState.CurrentState;
        if (currentState == CircuitState.HalfOpen)
        {
            _logger.LogInformation("Circuit breaker for {RequestName} is half-open. Testing service recovery with trial stream", requestName);
        }

        var channel = Channel.CreateUnbounded<TResponse>();
        var reader = channel.Reader;
        var writer = channel.Writer;
        var streamMetrics = new StreamingOperationMetrics();

        // Process stream with circuit breaker monitoring in background task
        var processingTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in next(cancellationToken))
                {
                    streamMetrics.ItemCount++;
                    
                    await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
                }

                // Stream completed successfully
                streamMetrics.CompletedSuccessfully = true;
                streamMetrics.EndTime = DateTime.UtcNow;

                // Record successful stream execution
                circuitState.RecordStreamResult(true, streamMetrics);

                if (currentState == CircuitState.HalfOpen)
                {
                    _logger.LogInformation("Trial stream for {RequestName} succeeded with {ItemCount} items. Circuit breaker may transition to closed state",
                        requestName, streamMetrics.ItemCount);
                }
            }
            catch (Exception ex)
            {
                streamMetrics.CompletedSuccessfully = false;
                streamMetrics.EndTime = DateTime.UtcNow;
                streamMetrics.LastError = ex;
                streamMetrics.ErrorCount = 1;

                // Record the stream failure
                circuitState.RecordStreamResult(false, streamMetrics, ex);

                var newState = circuitState.CurrentState;
                if (currentState != newState)
                {
                    _logger.LogWarning("Circuit breaker for {RequestName} transitioned from {OldState} to {NewState} due to stream failure after {ItemCount} items: {Exception}",
                        requestName, currentState, newState, streamMetrics.ItemCount, ex.Message);
                }
                else if (currentState == CircuitState.HalfOpen)
                {
                    _logger.LogWarning("Trial stream for {RequestName} failed after {ItemCount} items. Circuit breaker opened again: {Exception}",
                        requestName, streamMetrics.ItemCount, ex.Message);
                }

                throw; // Re-throw for proper error handling
            }
            finally
            {
                writer.Complete();
            }
        }, cancellationToken);

        // Yield items from channel - safe operation with no try-catch
        await foreach (var item in reader.ReadAllAsync(cancellationToken))
        {
            yield return item; // ← Completely safe yielding
        }

        // Ensure processing completed and handle any errors
        try
        {
            await processingTask;
        }
        catch (Exception ex)
        {
            throw new StreamingOperationException(
                $"Circuit breaker protected stream failed for {requestName}",
                streamMetrics.ItemCount,
                ex);
        }
    }
}