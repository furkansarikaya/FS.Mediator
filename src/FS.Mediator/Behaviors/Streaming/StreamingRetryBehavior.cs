using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using FS.Mediator.Core;
using FS.Mediator.Exceptions;
using FS.Mediator.Models.Enums;
using FS.Mediator.Models.Options;
using Microsoft.Extensions.Logging;

namespace FS.Mediator.Behaviors.Streaming;

/// <summary>
/// Streaming pipeline behavior that implements intelligent retry logic for stream operations.
/// 
/// Retry for streaming is complex because streams can partially succeed. This implementation
/// uses a "restart from beginning" strategy, which is the safest approach for most scenarios.
/// The channel-based pattern allows us to handle retry logic with full try-catch capability
/// while keeping yield statements completely separate.
/// </summary>
/// <typeparam name="TRequest">The type of streaming request</typeparam>
/// <typeparam name="TResponse">The type of each item in the stream</typeparam>
public class StreamingRetryBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    private readonly ILogger<StreamingRetryBehavior<TRequest, TResponse>> _logger;
    private readonly StreamingRetryOptions _options;

    public StreamingRetryBehavior(
        ILogger<StreamingRetryBehavior<TRequest, TResponse>> logger,
        StreamingRetryOptions options)
    {
        _logger = logger;
        _options = options;
    }

    public async IAsyncEnumerable<TResponse> HandleAsync(
        TRequest request, 
        StreamRequestHandlerDelegate<TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestName = typeof(TRequest).Name;
        var channel = Channel.CreateUnbounded<TResponse>();
        var reader = channel.Reader;
        var writer = channel.Writer;

        // Retry processing happens in background task with full error handling
        var retryTask = Task.Run(async () =>
        {
            var totalStopwatch = Stopwatch.StartNew();
            var totalItemsProcessed = 0L;
            var attemptNumber = 0;

            while (attemptNumber <= _options.MaxRetryAttempts)
            {
                try
                {
                    if (attemptNumber > 0)
                    {
                        _logger.LogInformation("Retrying stream {RequestName} (attempt {Attempt}/{MaxAttempts})",
                            requestName, attemptNumber + 1, _options.MaxRetryAttempts + 1);

                        var delay = CalculateRetryDelay(attemptNumber);
                        await Task.Delay(delay, cancellationToken);

                        // Check total timeout
                        if (totalStopwatch.Elapsed >= _options.MaxTotalRetryTime)
                        {
                            _logger.LogWarning("Stream {RequestName} exceeded maximum retry time ({MaxTime}ms)",
                                requestName, _options.MaxTotalRetryTime.TotalMilliseconds);
                            return; // Exit retry loop
                        }
                    }

                    var itemsInThisAttempt = 0L;

                    // Execute the stream attempt with full error handling
                    await foreach (var item in next(cancellationToken))
                    {
                        itemsInThisAttempt++;
                        totalItemsProcessed = itemsInThisAttempt; // For successful completion, this is the final count

                        await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
                    }

                    // If we reach here, the stream completed successfully
                    if (attemptNumber > 0)
                    {
                        _logger.LogInformation("Stream {RequestName} succeeded after {Attempts} attempts, processed {ItemCount} items",
                            requestName, attemptNumber + 1, totalItemsProcessed);
                    }

                    return; // Exit retry loop on success
                }
                catch (Exception ex)
                {
                    // Check if this exception should trigger a retry
                    if (!_options.ShouldRetryPredicate(ex))
                    {
                        _logger.LogDebug("Stream {RequestName} failed with non-retryable exception: {ExceptionType}",
                            requestName, ex.GetType().Name);
                        throw; // Non-retryable exception
                    }

                    // Check if we've exhausted retry attempts
                    if (attemptNumber >= _options.MaxRetryAttempts)
                    {
                        _logger.LogError("Stream {RequestName} failed after {MaxAttempts} attempts. Final exception: {Exception}",
                            requestName, _options.MaxRetryAttempts + 1, ex.Message);
                        
                        throw new StreamingOperationException(
                            $"Stream failed after {_options.MaxRetryAttempts + 1} attempts",
                            totalItemsProcessed,
                            ex);
                    }

                    _logger.LogWarning("Stream {RequestName} attempt {Attempt} failed with {ExceptionType}: {Message}. Will retry...",
                        requestName, attemptNumber + 1, ex.GetType().Name, ex.Message);

                    attemptNumber++;
                }
            }
        }, cancellationToken);

        // Start the retry task
        var processingTask = retryTask.ContinueWith(t =>
        {
            writer.Complete();
            if (t.IsFaulted && t.Exception != null)
            {
                throw t.Exception.GetBaseException();
            }
        }, cancellationToken);

        // Yield items from channel - completely safe, no error handling needed here
        await foreach (var item in reader.ReadAllAsync(cancellationToken))
        {
            yield return item; // ← Safe yielding with no try-catch
        }

        // Ensure processing completed and handle any errors
        await processingTask;
    }

    private TimeSpan CalculateRetryDelay(int attemptNumber)
    {
        return _options.RetryStrategy switch
        {
            RetryStrategy.FixedDelay => _options.InitialDelay,
            RetryStrategy.ExponentialBackoff => 
                TimeSpan.FromMilliseconds(_options.InitialDelay.TotalMilliseconds * Math.Pow(2, attemptNumber)),
            RetryStrategy.ExponentialBackoffWithJitter => 
                CalculateJitteredDelay(attemptNumber),
            _ => _options.InitialDelay
        };
    }

    private TimeSpan CalculateJitteredDelay(int attemptNumber)
    {
        var baseDelay = _options.InitialDelay.TotalMilliseconds * Math.Pow(2, attemptNumber);
        var jitterRange = baseDelay * 0.25;
        var jitter = (Random.Shared.NextDouble() - 0.5) * 2 * jitterRange;
        var finalDelay = Math.Max(0, baseDelay + jitter);
        return TimeSpan.FromMilliseconds(finalDelay);
    }
}
