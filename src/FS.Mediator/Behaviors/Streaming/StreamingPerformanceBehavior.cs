using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using FS.Mediator.Core;
using FS.Mediator.Models.Options;
using Microsoft.Extensions.Logging;

namespace FS.Mediator.Behaviors.Streaming;

/// <summary>
/// Streaming pipeline behavior that monitors performance and logs slow streams.
/// 
/// Performance monitoring for streams focuses on throughput (items/second), time to first item,
/// and total duration. This implementation uses the channel pattern to separate performance
/// monitoring logic from the yielding operations.
/// </summary>
/// <typeparam name="TRequest">The type of streaming request</typeparam>
/// <typeparam name="TResponse">The type of each item in the stream</typeparam>
public class StreamingPerformanceBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    private readonly ILogger<StreamingPerformanceBehavior<TRequest, TResponse>> _logger;
    private readonly StreamingPerformanceOptions _options;

    public StreamingPerformanceBehavior(
        ILogger<StreamingPerformanceBehavior<TRequest, TResponse>> logger,
        StreamingPerformanceOptions options)
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

        // Performance monitoring happens in background task with full error handling capability
        var monitoringTask = Task.Run(async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            long itemCount = 0;
            DateTime? firstItemTime = null;
            var lastPerformanceCheck = DateTime.UtcNow;

            try
            {
                await foreach (var item in next(cancellationToken))
                {
                    itemCount++;

                    // Record time to first item
                    if (firstItemTime == null)
                    {
                        firstItemTime = DateTime.UtcNow;
                        var timeToFirstItem = stopwatch.Elapsed;
                        
                        if (timeToFirstItem.TotalMilliseconds > _options.TimeToFirstItemWarningMs)
                        {
                            _logger.LogWarning("Slow time to first item for {RequestName}: {TimeToFirstItem}ms",
                                requestName, timeToFirstItem.TotalMilliseconds);
                        }
                    }

                    // Check throughput periodically
                    var now = DateTime.UtcNow;
                    if ((now - lastPerformanceCheck).TotalSeconds >= _options.ThroughputCheckIntervalSeconds)
                    {
                        var itemsPerSecond = itemCount / stopwatch.Elapsed.TotalSeconds;
                        
                        if (itemsPerSecond < _options.MinimumThroughputItemsPerSecond)
                        {
                            _logger.LogWarning("Low throughput detected for {RequestName}: {ItemsPerSecond:F1} items/sec (minimum: {MinThroughput})",
                                requestName, itemsPerSecond, _options.MinimumThroughputItemsPerSecond);
                        }
                        
                        lastPerformanceCheck = now;
                    }

                    // Write to channel
                    await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
                }

                // Final performance summary
                stopwatch.Stop();
                var totalDuration = stopwatch.Elapsed;
                var averageThroughput = itemCount > 0 ? itemCount / totalDuration.TotalSeconds : 0;

                if (totalDuration.TotalMilliseconds > _options.TotalDurationWarningMs)
                {
                    _logger.LogWarning("Long-running stream detected for {RequestName}: {Duration}ms, {ItemCount} items, {Throughput:F1} items/sec",
                        requestName, totalDuration.TotalMilliseconds, itemCount, averageThroughput);
                }
                else
                {
                    _logger.LogDebug("Stream {RequestName} completed: {Duration}ms, {ItemCount} items, {Throughput:F1} items/sec",
                        requestName, totalDuration.TotalMilliseconds, itemCount, averageThroughput);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Performance monitoring failed for stream {RequestName}", requestName);
                throw;
            }
            finally
            {
                writer.Complete();
            }
        }, cancellationToken);

        // Yield items from channel - completely safe with no error handling needed
        await foreach (var item in reader.ReadAllAsync(cancellationToken))
        {
            yield return item; // ← Safe yielding operation
        }

        // Ensure monitoring task completed
        await monitoringTask;
    }
}