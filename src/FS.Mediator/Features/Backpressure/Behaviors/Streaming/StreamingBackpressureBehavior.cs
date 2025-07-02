using System.Runtime.CompilerServices;
using System.Threading.Channels;
using FS.Mediator.Features.Backpressure.Models;
using FS.Mediator.Features.Backpressure.Models.Enums;
using FS.Mediator.Features.Backpressure.Models.Options;
using FS.Mediator.Features.StreamHandling.Core;
using Microsoft.Extensions.Logging;

namespace FS.Mediator.Features.Backpressure.Behaviors.Streaming;

/// <summary>
/// Streaming pipeline behavior that handles backpressure when consumers cannot keep up with producers.
/// 
/// Imagine you're managing a busy restaurant where orders come in faster than the kitchen can prepare them.
/// Without proper management, you'd have several problems:
/// 1. The order queue would grow infinitely (memory exhaustion)
/// 2. Customers would wait forever (poor user experience)  
/// 3. The kitchen would be overwhelmed (system instability)
/// 4. Eventually, the restaurant would collapse (system failure)
/// 
/// Backpressure management is like being a skilled maître d' who:
/// - **Monitors the Queue**: Watches how many orders are waiting
/// - **Measures Kitchen Speed**: Tracks how fast orders are being completed
/// - **Takes Action**: Implements strategies when the kitchen falls behind
/// - **Maintains Quality**: Ensures the restaurant continues operating smoothly
/// 
/// This behavior implements several sophisticated strategies for handling producer-consumer
/// speed mismatches, ensuring your streaming operations remain stable and predictable
/// even under extreme load conditions.
/// 
/// The key insight is that controlled degradation is always better than system failure.
/// It's better to intentionally drop some data or slow down processing than to have
/// your entire system crash from memory exhaustion.
/// </summary>
/// <typeparam name="TRequest">The type of streaming request</typeparam>
/// <typeparam name="TResponse">The type of each item in the stream</typeparam>
public class StreamingBackpressureBehavior<TRequest, TResponse>(
    ILogger<StreamingBackpressureBehavior<TRequest, TResponse>> logger,
    BackpressureOptions options) : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    private readonly ILogger<StreamingBackpressureBehavior<TRequest, TResponse>> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly BackpressureOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public async IAsyncEnumerable<TResponse> HandleAsync(
        TRequest request, 
        StreamRequestHandlerDelegate<TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestName = typeof(TRequest).Name;
        var sessionId = Guid.NewGuid().ToString("N")[..8];
        
        _logger.LogInformation("Starting backpressure management for {RequestName} with session {SessionId} using {Strategy} strategy",
            requestName, sessionId, _options.Strategy);
        
        // Create a bounded channel that acts as our "pressure valve"
        // The channel capacity is our buffer - this is where we queue items when consumer is slower than producer
        var channelOptions = new BoundedChannelOptions(_options.MaxBufferSize)
        {
            FullMode = BoundedChannelFullMode.Wait, // This creates natural backpressure
            SingleReader = true,  // Optimize for single consumer
            SingleWriter = true   // Optimize for single producer
        };
        
        var channel = Channel.CreateBounded<BackpressureWrapper<TResponse>>(channelOptions);
        var reader = channel.Reader;
        var writer = channel.Writer;
        
        // Initialize backpressure monitoring session
        var session = new BackpressureSession
        {
            SessionId = sessionId,
            RequestType = typeof(TRequest),
            StartTime = DateTime.UtcNow,
            Strategy = _options.Strategy,
            MaxBufferSize = _options.MaxBufferSize,
            HighWaterMark = (int)(_options.MaxBufferSize * _options.HighWaterMarkThreshold),
            LowWaterMark = (int)(_options.MaxBufferSize * _options.LowWaterMarkThreshold)
        };
        
        // Producer task manages the incoming stream and applies backpressure strategies
        var producerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in next(cancellationToken))
                {
                    // Update session metrics - this is our "restaurant monitoring system"
                    session.ItemsProduced++;
                    session.LastProducerActivity = DateTime.UtcNow;
                    
                    // Check if we need to apply backpressure based on current conditions
                    var backpressureNeeded = await ShouldApplyBackpressureAsync(session);
                    
                    if (backpressureNeeded)
                    {
                        // Apply the configured backpressure strategy
                        var handled = await ApplyBackpressureStrategyAsync(session, item, writer, cancellationToken);
                        
                        if (!handled)
                        {
                            // If item was dropped or rejected, log it and continue
                            session.ItemsDropped++;
                            continue;
                        }
                    }
                    else
                    {
                        // Normal operation - send item through the channel
                        var wrapper = new BackpressureWrapper<TResponse>
                        {
                            Item = item,
                            ProducedAt = DateTime.UtcNow,
                            SequenceNumber = session.ItemsProduced
                        };
                        
                        await writer.WriteAsync(wrapper, cancellationToken).ConfigureAwait(false);
                    }
                    
                    // Perform periodic monitoring and metrics collection
                    await PerformPeriodicMonitoringAsync(session);
                }
                
                // Producer completed successfully
                await LogProducerCompletionAsync(session);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Backpressure producer cancelled for session {SessionId} after producing {ItemCount} items",
                    session.SessionId, session.ItemsProduced);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backpressure producer failed for session {SessionId}", session.SessionId);
                throw;
            }
            finally
            {
                writer.Complete();
            }
        }, cancellationToken);
        
        // Consumer side - yield items from the channel while tracking consumption rates
        var lastConsumerActivity = DateTime.UtcNow;
        
        await foreach (var wrapper in reader.ReadAllAsync(cancellationToken))
        {
            // Update consumer metrics
            session.ItemsConsumed++;
            session.LastConsumerActivity = DateTime.UtcNow;
            lastConsumerActivity = DateTime.UtcNow;
            
            // Calculate and log latency if detailed metrics are enabled
            if (_options.CollectDetailedMetrics)
            {
                var latency = DateTime.UtcNow - wrapper.ProducedAt;
                session.TotalLatency += latency;
                
                if (latency > session.MaxLatency)
                {
                    session.MaxLatency = latency;
                }
            }
            
            yield return wrapper.Item; // ← Safe yielding with comprehensive backpressure protection
        }
        
        // Ensure producer task completed and handle any errors
        await producerTask;
        
        // Log final backpressure statistics
        await LogFinalBackpressureStatsAsync(session);
    }
    
    /// <summary>
    /// Determines whether backpressure should be applied based on current system conditions.
    /// This is like checking the restaurant's "vital signs" - queue length, kitchen speed, customer satisfaction.
    /// </summary>
    private Task<bool> ShouldApplyBackpressureAsync(BackpressureSession session)
    {
        // First, check the basic buffer size threshold
        var currentBufferEstimate = session.ItemsProduced - session.ItemsConsumed;
        
        // Primary trigger: buffer size approaching capacity
        if (currentBufferEstimate >= session.HighWaterMark)
        {
            if (!session.BackpressureActive)
            {
                _logger.LogWarning("Backpressure triggered for session {SessionId}: buffer size {BufferSize} exceeded high watermark {HighWaterMark}",
                    session.SessionId, currentBufferEstimate, session.HighWaterMark);
                
                session.BackpressureActive = true;
                session.BackpressureStartTime = DateTime.UtcNow;
            }
            
            return Task.FromResult(true);
        }
        
        // Hysteresis: only stop backpressure when well below the trigger point
        if (session.BackpressureActive && currentBufferEstimate <= session.LowWaterMark)
        {
            _logger.LogInformation("Backpressure relief for session {SessionId}: buffer size {BufferSize} dropped below low watermark {LowWaterMark}",
                session.SessionId, currentBufferEstimate, session.LowWaterMark);
            
            session.BackpressureActive = false;
            session.BackpressureEndTime = DateTime.UtcNow;
            
            if (session.BackpressureStartTime.HasValue)
            {
                var duration = session.BackpressureEndTime.Value - session.BackpressureStartTime.Value;
                session.TotalBackpressureTime += duration;
                
                _logger.LogInformation("Backpressure episode completed for session {SessionId}: duration {DurationMs}ms",
                    session.SessionId, duration.TotalMilliseconds);
            }
            
            return Task.FromResult(false);
        }
        
        // Custom trigger logic if provided
        if (_options.CustomBackpressureTrigger != null)
        {
            var metrics = CreateBackpressureMetrics(session);
            var customTrigger = _options.CustomBackpressureTrigger(metrics);
            
            if (customTrigger && !session.BackpressureActive)
            {
                _logger.LogInformation("Custom backpressure trigger activated for session {SessionId}", session.SessionId);
                session.BackpressureActive = true;
                session.BackpressureStartTime = DateTime.UtcNow;
                return Task.FromResult(true);
            }
        }
        
        return Task.FromResult(session.BackpressureActive);
    }
    
    /// <summary>
    /// Applies the configured backpressure strategy to handle the current item.
    /// This is where we implement the different "restaurant management" strategies.
    /// 
    /// Think of each strategy as a different management philosophy:
    /// - Buffer: "Let's take more orders and hope the kitchen catches up"
    /// - Drop: "Sorry, we're full - come back later"  
    /// - Throttle: "Please wait a moment before ordering"
    /// - Sample: "We're only taking every other order right now"
    /// - Block: "Kitchen is backed up - no new orders until we catch up"
    /// </summary>
    private async Task<bool> ApplyBackpressureStrategyAsync(
        BackpressureSession session, 
        TResponse item, 
        ChannelWriter<BackpressureWrapper<TResponse>> writer, 
        CancellationToken cancellationToken)
    {
        return _options.Strategy switch
        {
            BackpressureStrategy.Buffer => await ApplyBufferStrategyAsync(session, item, writer, cancellationToken),
            BackpressureStrategy.Drop => await ApplyDropStrategyAsync(session, item, writer, cancellationToken),
            BackpressureStrategy.Throttle => await ApplyThrottleStrategyAsync(session, item, writer, cancellationToken),
            BackpressureStrategy.Sample => await ApplySampleStrategyAsync(session, item, writer, cancellationToken),
            BackpressureStrategy.Block => await ApplyBlockStrategyAsync(session, item, writer, cancellationToken),
            _ => await ApplyBufferStrategyAsync(session, item, writer, cancellationToken) // Default fallback
        };
    }
    
    /// <summary>
    /// Buffer strategy: Continue queuing items but with awareness of limits.
    /// This is like a restaurant that keeps taking orders even when the kitchen is slow,
    /// hoping things will catch up. Risky but maintains customer satisfaction when it works.
    /// </summary>
    private async Task<bool> ApplyBufferStrategyAsync(
        BackpressureSession session, 
        TResponse item, 
        ChannelWriter<BackpressureWrapper<TResponse>> writer, 
        CancellationToken cancellationToken)
    {
        try
        {
            var wrapper = new BackpressureWrapper<TResponse>
            {
                Item = item,
                ProducedAt = DateTime.UtcNow,
                SequenceNumber = session.ItemsProduced,
                BackpressureApplied = true
            };
            
            // Try to write with a timeout to avoid infinite blocking
            var writeTask = writer.WriteAsync(wrapper, cancellationToken);
            var timeoutTask = Task.Delay(1000, cancellationToken); // 1 second timeout
            
            var completedTask = await Task.WhenAny(writeTask.AsTask(), timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                // Timeout occurred - buffer is likely full
                _logger.LogWarning("Buffer strategy timeout for session {SessionId} - buffer may be full", session.SessionId);
                return false; // Item not handled
            }
            
            await writeTask; // Ensure the write completed successfully
            return true; // Item successfully buffered
        }
        catch (InvalidOperationException)
        {
            // Channel was closed
            return false;
        }
    }
    
    /// <summary>
    /// Drop strategy: Discard items when under pressure.
    /// This is like a restaurant that stops taking orders when the kitchen is overwhelmed.
    /// Maintains system stability at the cost of losing some data.
    /// </summary>
    private async Task<bool> ApplyDropStrategyAsync(
        BackpressureSession session, 
        TResponse item, 
        ChannelWriter<BackpressureWrapper<TResponse>> writer, 
        CancellationToken cancellationToken)
    {
        var currentBufferEstimate = session.ItemsProduced - session.ItemsConsumed;
        
        // Decide whether to drop based on buffer pressure and configuration
        var shouldDrop = currentBufferEstimate >= session.MaxBufferSize;
        
        if (shouldDrop)
        {
            // Implement drop preference logic
            if (_options.PreferNewerItems)
            {
                // Drop this item (older items in buffer are preserved)
                _logger.LogDebug("Dropping current item for session {SessionId} - preferring newer items in buffer", session.SessionId);
                return false;
            }
            else
            {
                // Try to make room by dropping older items (this is conceptual - actual implementation would need buffer access)
                _logger.LogDebug("Attempting to drop older items for session {SessionId} - preferring current item", session.SessionId);
                // For simplicity, we'll still drop the current item in this implementation
                return false;
            }
        }
        
        // Not dropping - send the item through
        var wrapper = new BackpressureWrapper<TResponse>
        {
            Item = item,
            ProducedAt = DateTime.UtcNow,
            SequenceNumber = session.ItemsProduced,
            BackpressureApplied = true
        };
        
        await writer.WriteAsync(wrapper, cancellationToken).ConfigureAwait(false);
        return true;
    }
    
    /// <summary>
    /// Throttle strategy: Slow down the producer by introducing delays.
    /// This is like asking customers to wait before placing orders when the kitchen is busy.
    /// Maintains data completeness but reduces overall throughput.
    /// </summary>
    private async Task<bool> ApplyThrottleStrategyAsync(
        BackpressureSession session, 
        TResponse item, 
        ChannelWriter<BackpressureWrapper<TResponse>> writer, 
        CancellationToken cancellationToken)
    {
        var currentBufferEstimate = session.ItemsProduced - session.ItemsConsumed;
        
        // Calculate delay based on buffer pressure
        // More pressure = longer delay, up to the maximum configured delay
        var bufferPressure = Math.Min(1.0, (double)currentBufferEstimate / session.MaxBufferSize);
        var delayMs = (int)(bufferPressure * _options.MaxThrottleDelayMs);
        
        if (delayMs > 0)
        {
            _logger.LogDebug("Applying throttle delay of {DelayMs}ms for session {SessionId} (buffer pressure: {Pressure:P1})",
                delayMs, session.SessionId, bufferPressure);
            
            await Task.Delay(delayMs, cancellationToken);
            session.TotalThrottleDelay += TimeSpan.FromMilliseconds(delayMs);
        }
        
        // After delay, send the item through
        var wrapper = new BackpressureWrapper<TResponse>
        {
            Item = item,
            ProducedAt = DateTime.UtcNow,
            SequenceNumber = session.ItemsProduced,
            BackpressureApplied = true,
            ThrottleDelayApplied = TimeSpan.FromMilliseconds(delayMs)
        };
        
        await writer.WriteAsync(wrapper, cancellationToken).ConfigureAwait(false);
        return true;
    }
    
    /// <summary>
    /// Sample strategy: Only process a subset of items when under pressure.
    /// This is like a restaurant that only takes every nth order when the kitchen is overwhelmed.
    /// Maintains system responsiveness but intentionally loses data for statistical processing.
    /// </summary>
    private async Task<bool> ApplySampleStrategyAsync(
        BackpressureSession session, 
        TResponse item, 
        ChannelWriter<BackpressureWrapper<TResponse>> writer, 
        CancellationToken cancellationToken)
    {
        // Determine if this item should be sampled (processed) or skipped
        var shouldSample = session.ItemsProduced % _options.SampleRate == 0;
        
        if (!shouldSample)
        {
            _logger.LogDebug("Sampling: skipping item {SequenceNumber} for session {SessionId} (rate: 1/{SampleRate})",
                session.ItemsProduced, session.SessionId, _options.SampleRate);
            session.ItemsSampled++;
            return false; // Item not processed
        }
        
        // This item is part of the sample - process it
        var wrapper = new BackpressureWrapper<TResponse>
        {
            Item = item,
            ProducedAt = DateTime.UtcNow,
            SequenceNumber = session.ItemsProduced,
            BackpressureApplied = true,
            WasSampled = true
        };
        
        await writer.WriteAsync(wrapper, cancellationToken).ConfigureAwait(false);
        return true;
    }
    
    /// <summary>
    /// Block strategy: Completely halt producer until consumer catches up.
    /// This is like a restaurant that stops taking any orders until the kitchen clears its backlog.
    /// Ensures no data loss but can significantly impact throughput.
    /// </summary>
    private async Task<bool> ApplyBlockStrategyAsync(
        BackpressureSession session, 
        TResponse item, 
        ChannelWriter<BackpressureWrapper<TResponse>> writer, 
        CancellationToken cancellationToken)
    {
        // Wait until buffer pressure reduces significantly
        var startTime = DateTime.UtcNow;
        
        while (session.ItemsProduced - session.ItemsConsumed >= session.LowWaterMark)
        {
            _logger.LogDebug("Blocking producer for session {SessionId} - waiting for consumer to catch up", session.SessionId);
            
            await Task.Delay(100, cancellationToken); // Check every 100ms
            
            // Safety timeout to prevent infinite blocking
            if (DateTime.UtcNow - startTime > TimeSpan.FromSeconds(30))
            {
                _logger.LogWarning("Block strategy timeout for session {SessionId} - proceeding anyway", session.SessionId);
                break;
            }
        }
        
        var blockDuration = DateTime.UtcNow - startTime;
        session.TotalBlockTime += blockDuration;
        
        // After blocking period, send the item through
        var wrapper = new BackpressureWrapper<TResponse>
        {
            Item = item,
            ProducedAt = DateTime.UtcNow,
            SequenceNumber = session.ItemsProduced,
            BackpressureApplied = true,
            BlockDelayApplied = blockDuration
        };
        
        await writer.WriteAsync(wrapper, cancellationToken).ConfigureAwait(false);
        return true;
    }
    
    /// <summary>
    /// Performs periodic monitoring of backpressure metrics and system health.
    /// This is like a restaurant manager doing regular rounds to check on operations.
    /// </summary>
    private async Task PerformPeriodicMonitoringAsync(BackpressureSession session)
    {
        var now = DateTime.UtcNow;
        
        // Only perform detailed monitoring every few seconds to avoid overhead
        if ((now - session.LastMonitoringCheck).TotalSeconds < 5)
        {
            return;
        }
        
        session.LastMonitoringCheck = now;
        
        // Calculate current rates
        var timeWindow = TimeSpan.FromSeconds(_options.MeasurementWindowSeconds);
        var windowStart = now - timeWindow;
        
        // Estimate current rates (in a real implementation, you'd track these more precisely)
        var recentDuration = now - session.StartTime;
        var producerRate = recentDuration.TotalSeconds > 0 ? session.ItemsProduced / recentDuration.TotalSeconds : 0;
        var consumerRate = recentDuration.TotalSeconds > 0 ? session.ItemsConsumed / recentDuration.TotalSeconds : 0;
        
        // Log current status if detailed metrics are enabled
        if (_options.CollectDetailedMetrics)
        {
            var currentBuffer = session.ItemsProduced - session.ItemsConsumed;
            var bufferUtilization = (double)currentBuffer / session.MaxBufferSize;
            
            _logger.LogDebug("Backpressure monitoring for session {SessionId}: " +
                            "Buffer={BufferSize}/{MaxSize} ({Utilization:P1}), " +
                            "Producer={ProducerRate:F1}/s, Consumer={ConsumerRate:F1}/s, " +
                            "Active={BackpressureActive}",
                session.SessionId, currentBuffer, session.MaxBufferSize, bufferUtilization,
                producerRate, consumerRate, session.BackpressureActive);
        }
        
        // Execute custom backpressure handler if provided and backpressure is active
        if (session.BackpressureActive && _options.CustomBackpressureHandler != null)
        {
            try
            {
                var context = new BackpressureContext
                {
                    Strategy = session.Strategy,
                    Metrics = CreateBackpressureMetrics(session),
                    RequestType = session.RequestType,
                    TriggeredAt = session.BackpressureStartTime ?? now
                };
                
                _options.CustomBackpressureHandler(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Custom backpressure handler failed for session {SessionId}", session.SessionId);
            }
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Creates a BackpressureMetrics object with current session data.
    /// This provides a snapshot of the current system state for decision making.
    /// </summary>
    private BackpressureMetrics CreateBackpressureMetrics(BackpressureSession session)
    {
        var now = DateTime.UtcNow;
        var duration = now - session.StartTime;
        var currentBuffer = (int)(session.ItemsProduced - session.ItemsConsumed);
        
        var producerRate = duration.TotalSeconds > 0 ? session.ItemsProduced / duration.TotalSeconds : 0;
        var consumerRate = duration.TotalSeconds > 0 ? session.ItemsConsumed / duration.TotalSeconds : 0;
        
        var backpressureDuration = session.BackpressureActive && session.BackpressureStartTime.HasValue
            ? now - session.BackpressureStartTime.Value
            : (TimeSpan?)null;
        
        return new BackpressureMetrics
        {
            CurrentBufferSize = currentBuffer,
            MaxBufferSize = session.MaxBufferSize,
            ProducerRate = producerRate,
            ConsumerRate = consumerRate,
            BackpressureDuration = backpressureDuration,
            MemoryUsage = GC.GetTotalMemory(false),
            CustomMetrics = new Dictionary<string, object>
            {
                ["SessionId"] = session.SessionId,
                ["Strategy"] = session.Strategy.ToString(),
                ["ItemsDropped"] = session.ItemsDropped,
                ["ItemsSampled"] = session.ItemsSampled,
                ["TotalThrottleDelay"] = session.TotalThrottleDelay,
                ["TotalBlockTime"] = session.TotalBlockTime
            }
        };
    }
    
    /// <summary>
    /// Logs producer completion statistics for analysis and monitoring.
    /// </summary>
    private async Task LogProducerCompletionAsync(BackpressureSession session)
    {
        var completionStats = new
        {
            SessionId = session.SessionId,
            Strategy = session.Strategy.ToString(),
            TotalDurationSeconds = (DateTime.UtcNow - session.StartTime).TotalSeconds,
            ItemsProduced = session.ItemsProduced,
            ItemsConsumed = session.ItemsConsumed,
            ItemsDropped = session.ItemsDropped,
            ItemsSampled = session.ItemsSampled,
            BackpressureActiveTime = session.TotalBackpressureTime.TotalSeconds,
            TotalThrottleDelay = session.TotalThrottleDelay.TotalSeconds,
            TotalBlockTime = session.TotalBlockTime.TotalSeconds,
            EffectivenessScore = CalculateBackpressureEffectiveness(session)
        };
        
        _logger.LogInformation("Backpressure producer completed: {@CompletionStats}", completionStats);
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Logs final backpressure statistics when the stream completes.
    /// This provides a comprehensive summary for performance analysis.
    /// </summary>
    private async Task LogFinalBackpressureStatsAsync(BackpressureSession session)
    {
        var finalStats = new
        {
            SessionId = session.SessionId,
            RequestType = session.RequestType.Name,
            Strategy = session.Strategy.ToString(),
            Summary = new
            {
                TotalDurationMinutes = (DateTime.UtcNow - session.StartTime).TotalMinutes,
                ItemsProduced = session.ItemsProduced,
                ItemsConsumed = session.ItemsConsumed,
                SuccessRate = session.ItemsProduced > 0 ? (double)session.ItemsConsumed / session.ItemsProduced : 0,
                BackpressureEffectiveness = CalculateBackpressureEffectiveness(session)
            },
            StrategySpecificMetrics = GetStrategySpecificMetrics(session),
            PerformanceMetrics = _options.CollectDetailedMetrics ? new
            {
                AverageLatencyMs = session.ItemsConsumed > 0 ? session.TotalLatency.TotalMilliseconds / session.ItemsConsumed : 0,
                MaxLatencyMs = session.MaxLatency.TotalMilliseconds,
                BackpressureActivePercentage = (DateTime.UtcNow - session.StartTime).TotalSeconds > 0 ? 
                    session.TotalBackpressureTime.TotalSeconds / (DateTime.UtcNow - session.StartTime).TotalSeconds * 100 : 0
            } : null
        };
        
        _logger.LogInformation("Final backpressure statistics: {@FinalStats}", finalStats);
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Calculates the effectiveness of backpressure handling for this session.
    /// Returns a score from 0 (completely ineffective) to 100 (perfectly effective).
    /// </summary>
    private double CalculateBackpressureEffectiveness(BackpressureSession session)
    {
        var score = 100.0;
        
        // Penalize data loss (for strategies where it's not expected)
        if (_options.Strategy != BackpressureStrategy.Drop && _options.Strategy != BackpressureStrategy.Sample)
        {
            if (session.ItemsProduced > 0)
            {
                var dataLossRate = (double)session.ItemsDropped / session.ItemsProduced;
                score -= dataLossRate * 50; // Up to 50 point penalty for unexpected data loss
            }
        }
        
        // Penalize excessive delays (for strategies that shouldn't cause them)
        if (_options.Strategy != BackpressureStrategy.Throttle && _options.Strategy != BackpressureStrategy.Block)
        {
            var totalDelaySeconds = session.TotalThrottleDelay.TotalSeconds + session.TotalBlockTime.TotalSeconds;
            if (totalDelaySeconds > 0)
            {
                score -= Math.Min(30, totalDelaySeconds / 10); // Up to 30 point penalty for delays
            }
        }
        
        // Reward stability (less time in backpressure mode is better)
        var totalDuration = (DateTime.UtcNow - session.StartTime).TotalSeconds;
        if (!(totalDuration > 0)) return Math.Max(0, score);
        var backpressurePercentage = session.TotalBackpressureTime.TotalSeconds / totalDuration;
        if (backpressurePercentage > 0.5) // More than 50% of time in backpressure
        {
            score -= (backpressurePercentage - 0.5) * 40; // Up to 20 point penalty
        }

        return Math.Max(0, score);
    }
    
    /// <summary>
    /// Gets strategy-specific metrics based on the backpressure strategy used.
    /// Different strategies have different relevant metrics to track.
    /// </summary>
    private object GetStrategySpecificMetrics(BackpressureSession session)
    {
        return session.Strategy switch
        {
            BackpressureStrategy.Drop => new
            {
                ItemsDropped = session.ItemsDropped,
                DropRate = session.ItemsProduced > 0 ? (double)session.ItemsDropped / session.ItemsProduced : 0,
                PreferNewerItems = _options.PreferNewerItems
            },
            
            BackpressureStrategy.Throttle => new
            {
                TotalThrottleDelaySeconds = session.TotalThrottleDelay.TotalSeconds,
                MaxThrottleDelayMs = _options.MaxThrottleDelayMs,
                AverageDelayPerItem = session.ItemsProduced > 0 ? session.TotalThrottleDelay.TotalMilliseconds / session.ItemsProduced : 0
            },
            
            BackpressureStrategy.Sample => new
            {
                ItemsSampled = session.ItemsSampled,
                SampleRate = _options.SampleRate,
                EffectiveSamplePercentage = session.ItemsProduced > 0 ? (double)session.ItemsSampled / session.ItemsProduced : 0
            },
            
            BackpressureStrategy.Block => new
            {
                TotalBlockTimeSeconds = session.TotalBlockTime.TotalSeconds,
                BlockEvents = session.TotalBlockTime.TotalSeconds > 0 ? "Multiple" : "None",
                AverageBlockDelay = session.ItemsProduced > 0 ? session.TotalBlockTime.TotalMilliseconds / session.ItemsProduced : 0
            },
            
            _ => new { Strategy = session.Strategy.ToString() }
        };
    }
}

/// <summary>
/// Wrapper class that carries additional metadata about items flowing through the backpressure system.
/// This is like attaching a "receipt" to each order in our restaurant that tracks when it was created
/// and what processing it has undergone.
/// </summary>
internal class BackpressureWrapper<T>
{
    public T Item { get; set; } = default!;
    public DateTime ProducedAt { get; set; }
    public long SequenceNumber { get; set; }
    public bool BackpressureApplied { get; set; }
    public bool WasSampled { get; set; }
    public TimeSpan? ThrottleDelayApplied { get; set; }
    public TimeSpan? BlockDelayApplied { get; set; }
}

/// <summary>
/// Tracks the state and metrics for a single backpressure management session.
/// This is like the manager's logbook that records everything happening in the restaurant.
/// </summary>
internal class BackpressureSession
{
    public string SessionId { get; set; } = string.Empty;
    public Type RequestType { get; set; } = typeof(object);
    public DateTime StartTime { get; set; }
    public BackpressureStrategy Strategy { get; set; }
    public int MaxBufferSize { get; set; }
    public int HighWaterMark { get; set; }
    public int LowWaterMark { get; set; }
    
    // Production metrics
    public long ItemsProduced { get; set; }
    public DateTime LastProducerActivity { get; set; }
    
    // Consumption metrics  
    public long ItemsConsumed { get; set; }
    public DateTime LastConsumerActivity { get; set; }
    
    // Backpressure state
    public bool BackpressureActive { get; set; }
    public DateTime? BackpressureStartTime { get; set; }
    public DateTime? BackpressureEndTime { get; set; }
    public TimeSpan TotalBackpressureTime { get; set; }
    
    // Strategy-specific metrics
    public long ItemsDropped { get; set; }
    public long ItemsSampled { get; set; }
    public TimeSpan TotalThrottleDelay { get; set; }
    public TimeSpan TotalBlockTime { get; set; }
    
    // Performance metrics
    public TimeSpan TotalLatency { get; set; }
    public TimeSpan MaxLatency { get; set; }
    public DateTime LastMonitoringCheck { get; set; }
}