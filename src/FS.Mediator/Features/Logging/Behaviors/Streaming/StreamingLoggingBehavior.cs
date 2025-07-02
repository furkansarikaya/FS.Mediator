using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using FS.Mediator.Features.Logging.Models.Options;
using FS.Mediator.Features.StreamHandling.Core;
using FS.Mediator.Features.StreamHandling.Exceptions;
using FS.Mediator.Features.StreamHandling.Models;
using Microsoft.Extensions.Logging;

namespace FS.Mediator.Features.Logging.Behaviors.Streaming;

/// <summary>
/// Streaming pipeline behavior that provides comprehensive logging for stream operations.
/// 
/// This implementation uses a Channel-based pattern to completely separate error handling
/// from yielding operations. The key insight is that we process the stream in a background
/// task (where we can use try-catch), and then yield items from a channel (no try-catch needed).
/// 
/// This approach ensures we never have yield return statements inside try-catch blocks,
/// which is a fundamental C# language constraint.
/// </summary>
/// <typeparam name="TRequest">The type of streaming request</typeparam>
/// <typeparam name="TResponse">The type of each item in the stream</typeparam>
public class StreamingLoggingBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    private readonly ILogger<StreamingLoggingBehavior<TRequest, TResponse>> _logger;
    private readonly StreamingLoggingOptions _options;

    public StreamingLoggingBehavior(
        ILogger<StreamingLoggingBehavior<TRequest, TResponse>> logger,
        StreamingLoggingOptions? options = null)
    {
        _logger = logger;
        _options = options ?? new StreamingLoggingOptions();
    }

    public async IAsyncEnumerable<TResponse> HandleAsync(
        TRequest request, 
        StreamRequestHandlerDelegate<TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestName = typeof(TRequest).Name;
        var correlationId = Guid.NewGuid().ToString("N")[..8];
        
        // Create an unbounded channel to separate processing from yielding
        var channel = Channel.CreateUnbounded<TResponse>();
        var reader = channel.Reader;
        var writer = channel.Writer;
        
        // Metrics tracking for the stream
        var metrics = new StreamProcessingMetrics
        {
            StartTime = DateTime.UtcNow,
            CorrelationId = correlationId,
            RequestName = requestName
        };

        _logger.LogInformation("Starting stream processing for {RequestName} with correlation {CorrelationId}", 
            requestName, correlationId);

        // Background task processes the stream with full error handling capability
        var processingTask = Task.Run(async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            var lastProgressLog = DateTime.UtcNow;

            try
            {
                // Process the actual stream with try-catch (safe because no yield here)
                await foreach (var item in next(cancellationToken))
                {
                    metrics.ItemCount++;

                    // Log periodic progress to provide visibility into long-running operations
                    var now = DateTime.UtcNow;
                    if (ShouldLogProgress(metrics.ItemCount, now, lastProgressLog))
                    {
                        var elapsed = stopwatch.Elapsed;
                        var itemsPerSecond = metrics.ItemCount / elapsed.TotalSeconds;
                        
                        _logger.LogDebug("Stream {RequestName} progress: {ItemCount} items processed in {Elapsed}ms ({ItemsPerSecond:F1} items/sec) - Correlation: {CorrelationId}",
                            requestName, metrics.ItemCount, elapsed.TotalMilliseconds, itemsPerSecond, correlationId);
                        
                        lastProgressLog = now;
                    }

                    // Write item to channel (this can throw but we handle it)
                    await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
                }

                // Stream completed successfully
                stopwatch.Stop();
                metrics.EndTime = DateTime.UtcNow;
                metrics.CompletedSuccessfully = true;
                
                var finalRate = metrics.ItemCount > 0 ? metrics.ItemCount / stopwatch.Elapsed.TotalSeconds : 0;
                _logger.LogInformation("Stream {RequestName} completed successfully: {ItemCount} items in {Duration}ms ({ItemsPerSecond:F1} items/sec) - Correlation: {CorrelationId}",
                    requestName, metrics.ItemCount, stopwatch.ElapsedMilliseconds, finalRate, correlationId);
            }
            catch (Exception ex)
            {
                // Full error handling capability here since we're not using yield
                stopwatch.Stop();
                metrics.EndTime = DateTime.UtcNow;
                metrics.LastError = ex;
                metrics.CompletedSuccessfully = false;

                _logger.LogError(ex, "Stream {RequestName} failed after processing {ItemCount} items in {Duration}ms - Correlation: {CorrelationId}",
                    requestName, metrics.ItemCount, stopwatch.ElapsedMilliseconds, correlationId);
            }
            finally
            {
                // Always close the channel to signal completion
                writer.Complete();
            }
        }, cancellationToken);

        // Yield items from channel - this is completely safe and has no try-catch
        await foreach (var item in reader.ReadAllAsync(cancellationToken))
        {
            yield return item; // ← Completely safe - no try-catch anywhere in this path
        }

        // Wait for processing task to complete and handle any final errors
        try
        {
            await processingTask;
        }
        catch (Exception ex)
        {
            // If the processing task failed catastrophically, rethrow as streaming exception
            throw new StreamingOperationException(
                $"Stream processing task failed for {requestName}",
                metrics.ItemCount,
                ex);
        }

        // If stream failed, throw the original exception
        if (!metrics.CompletedSuccessfully && metrics.LastError != null)
        {
            throw metrics.LastError;
        }
    }

    /// <summary>
    /// Determines whether progress should be logged based on configured intervals.
    /// This prevents log spam while ensuring visibility into long-running operations.
    /// </summary>
    private bool ShouldLogProgress(long itemCount, DateTime now, DateTime lastProgressLog)
    {
        if (_options.LogProgressEveryNItems > 0 && itemCount % _options.LogProgressEveryNItems == 0)
            return true;

        return _options.LogProgressEveryNSeconds > 0 && 
               (now - lastProgressLog).TotalSeconds >= _options.LogProgressEveryNSeconds;
    }
}
