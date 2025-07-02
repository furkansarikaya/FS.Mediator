using System.Runtime;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using FS.Mediator.Features.ResourceManagement.Models;
using FS.Mediator.Features.ResourceManagement.Models.Enums;
using FS.Mediator.Features.ResourceManagement.Models.Options;
using FS.Mediator.Features.StreamHandling.Core;
using Microsoft.Extensions.Logging;

namespace FS.Mediator.Features.ResourceManagement.Behaviors.Streaming;

/// <summary>
/// Streaming pipeline behavior that manages system resources during stream processing.
/// 
/// Streaming resource management is like being a careful shepherd watching over a flock
/// that's constantly moving. Unlike regular requests that have a clear beginning and end,
/// streams can run for hours or even days, making resource management absolutely critical.
/// 
/// Think of this behavior as your stream's "resource bodyguard" that:
/// 1. **Monitors Continuously**: Tracks memory usage as data flows through
/// 2. **Prevents Accumulation**: Ensures resources don't build up over time
/// 3. **Responds Proactively**: Takes action before problems become critical
/// 4. **Maintains Flow**: Keeps the stream healthy without interrupting data flow
/// 
/// This is especially crucial for:
/// - Large data processing operations that handle millions of records
/// - Real-time data streams that run continuously
/// - ETL operations that transform massive datasets
/// - Long-running analytics processes
/// 
/// The behavior uses the channel pattern to ensure resource management logic
/// is completely separated from the yielding operations, maintaining stream performance.
/// </summary>
/// <typeparam name="TRequest">The type of streaming request</typeparam>
/// <typeparam name="TResponse">The type of each item in the stream</typeparam>
public class StreamingResourceManagementBehavior<TRequest, TResponse>(
    ILogger<StreamingResourceManagementBehavior<TRequest, TResponse>> logger,
    ResourceManagementOptions options) : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    private readonly ILogger<StreamingResourceManagementBehavior<TRequest, TResponse>> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ResourceManagementOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public async IAsyncEnumerable<TResponse> HandleAsync(
        TRequest request, 
        StreamRequestHandlerDelegate<TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestName = typeof(TRequest).Name;
        var sessionId = Guid.NewGuid().ToString("N")[..8];
        
        // Create a monitoring session specifically for this stream
        var session = new StreamingResourceSession
        {
            SessionId = sessionId,
            RequestType = typeof(TRequest),
            StartTime = DateTime.UtcNow,
            StartMemoryUsage = GC.GetTotalMemory(false),
            LastResourceCheck = DateTime.UtcNow
        };
        
        _logger.LogInformation("Starting streaming resource management for {RequestName} with session {SessionId}. Initial memory: {MemoryMB}MB",
            requestName, sessionId, session.StartMemoryUsage / 1_000_000);
        
        // Create channel for separating resource management from yielding
        var channel = Channel.CreateUnbounded<TResponse>();
        var reader = channel.Reader;
        var writer = channel.Writer;

        // Resource management task runs in background with full error handling capability
        var resourceManagementTask = Task.Run(async () =>
        {
            try
            {
                // Process the stream while continuously managing resources
                await foreach (var item in next(cancellationToken))
                {
                    // Increment item counter for tracking throughput
                    session.ItemsProcessed++;
                    
                    // Perform periodic resource checks - this is the heart of streaming resource management
                    var now = DateTime.UtcNow;
                    if ((now - session.LastResourceCheck).TotalSeconds >= _options.MonitoringIntervalSeconds)
                    {
                        await PerformPeriodicResourceCheckAsync(session);
                        session.LastResourceCheck = now;
                    }
                    
                    // Check for memory pressure on every Nth item to avoid overhead
                    if (session.ItemsProcessed % 1000 == 0) // Check every 1000 items
                    {
                        await CheckMemoryPressureAsync(session);
                    }
                    
                    // Write item to channel for consumption
                    await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
                }
                
                // Stream completed successfully - perform final resource assessment
                await PerformFinalResourceAssessmentAsync(session);
                
                _logger.LogInformation("Streaming resource management completed for session {SessionId}. Items processed: {ItemCount}, Final memory: {MemoryMB}MB",
                    session.SessionId, session.ItemsProcessed, GC.GetTotalMemory(false) / 1_000_000);
            }
            catch (OperationCanceledException)
            {
                // Stream was cancelled - this is normal, perform cleanup
                await PerformResourceCleanupAsync(session, "Stream cancelled");
                
                _logger.LogInformation("Streaming resource management cancelled for session {SessionId} after processing {ItemCount} items",
                    session.SessionId, session.ItemsProcessed);
            }
            catch (Exception ex)
            {
                // Stream failed - check if resource pressure contributed to the failure
                await PerformResourceCleanupAsync(session, "Stream failed");
                
                var currentMemory = GC.GetTotalMemory(false);
                var memoryGrowth = currentMemory - session.StartMemoryUsage;
                
                _logger.LogError(ex, "Streaming resource management detected stream failure for session {SessionId}. " +
                                    "Items processed: {ItemCount}, Memory growth: {MemoryGrowthMB}MB. " +
                                    "Resource pressure may have contributed to failure",
                    session.SessionId, session.ItemsProcessed, memoryGrowth / 1_000_000);
                
                throw; // Re-throw to maintain error semantics
            }
            finally
            {
                // Always close the channel to signal completion
                writer.Complete();
            }
        }, cancellationToken);

        // Yield items from channel - this is completely safe and has no error handling
        // All resource management happens in the background task
        await foreach (var item in reader.ReadAllAsync(cancellationToken))
        {
            yield return item; // ← Safe yielding with comprehensive resource management in background
        }

        // Ensure resource management task completed and handle any errors
        await resourceManagementTask;
    }
    
    /// <summary>
    /// Performs periodic resource assessment during stream processing.
    /// This is like taking the "vital signs" of your stream at regular intervals.
    /// 
    /// Unlike one-time requests, streams need continuous monitoring because:
    /// - Memory can accumulate gradually over thousands of items
    /// - Resource leaks compound over time
    /// - Performance can degrade slowly without notice
    /// - Early intervention prevents catastrophic failures
    /// </summary>
    private async Task PerformPeriodicResourceCheckAsync(StreamingResourceSession session)
    {
        var currentMemory = GC.GetTotalMemory(false);
        var memoryGrowth = currentMemory - session.StartMemoryUsage;
        var timeElapsed = DateTime.UtcNow - session.StartTime;
        var memoryGrowthRate = timeElapsed.TotalSeconds > 0 ? memoryGrowth / timeElapsed.TotalSeconds : 0;
        var itemsPerSecond = timeElapsed.TotalSeconds > 0 ? session.ItemsProcessed / timeElapsed.TotalSeconds : 0;
        
        // Update session metrics
        session.CurrentMemoryUsage = currentMemory;
        session.TotalMemoryGrowth = memoryGrowth;
        session.MemoryGrowthRate = memoryGrowthRate;
        
        // Calculate memory per item to detect efficiency issues
        var memoryPerItem = session.ItemsProcessed > 0 ? memoryGrowth / session.ItemsProcessed : 0;
        
        _logger.LogDebug("Periodic resource check for streaming session {SessionId}: " +
                        "Memory={MemoryMB}MB (+{GrowthMB}MB), Rate={RateMB}MB/s, " +
                        "Items={ItemCount} ({ItemsPerSec:F1}/s), Memory/Item={MemoryPerItemBytes}B",
            session.SessionId, currentMemory / 1_000_000, memoryGrowth / 1_000_000, 
            memoryGrowthRate / 1_000_000, session.ItemsProcessed, itemsPerSecond, memoryPerItem);
        
        // Check for concerning trends that might indicate problems
        await DetectResourceTrendsAsync(session);
        
        // Detailed diagnostics if enabled
        if (_options.CollectDetailedMemoryStats)
        {
            await LogDetailedStreamingMemoryStatsAsync(session);
        }
    }
    
    /// <summary>
    /// Checks for immediate memory pressure that requires action.
    /// This is like checking if your car's engine is overheating - if it is, you need to pull over now.
    /// </summary>
    private async Task CheckMemoryPressureAsync(StreamingResourceSession session)
    {
        var currentMemory = GC.GetTotalMemory(false);
        
        // Check absolute memory threshold
        if (currentMemory > _options.MaxMemoryThresholdBytes)
        {
            var memoryGrowth = currentMemory - session.StartMemoryUsage;
            
            _logger.LogWarning("Memory pressure detected in streaming session {SessionId}: " +
                              "Current memory {MemoryMB}MB exceeds threshold {ThresholdMB}MB. " +
                              "Growth since start: {GrowthMB}MB after {ItemCount} items",
                session.SessionId, currentMemory / 1_000_000, 
                _options.MaxMemoryThresholdBytes / 1_000_000, memoryGrowth / 1_000_000, 
                session.ItemsProcessed);
            
            await HandleStreamingResourcePressureAsync(session);
        }
        
        // Check growth rate if we have enough data points
        if (session.ItemsProcessed > 100) // Only check after processing some items
        {
            var timeElapsed = DateTime.UtcNow - session.StartTime;
            var currentGrowthRate = timeElapsed.TotalSeconds > 0 ? 
                (currentMemory - session.StartMemoryUsage) / timeElapsed.TotalSeconds : 0;
            
            if (currentGrowthRate > _options.MemoryGrowthRateThresholdBytesPerSecond)
            {
                _logger.LogWarning("High memory growth rate detected in streaming session {SessionId}: " +
                                  "{RateMB}MB/s exceeds threshold {ThresholdMB}MB/s. " +
                                  "This may indicate a memory leak in stream processing",
                    session.SessionId, currentGrowthRate / 1_000_000, 
                    _options.MemoryGrowthRateThresholdBytesPerSecond / 1_000_000);
                
                await HandleStreamingResourcePressureAsync(session);
            }
        }
    }
    
    /// <summary>
    /// Detects concerning resource trends that might indicate developing problems.
    /// This is like a doctor noticing that your blood pressure has been gradually increasing -
    /// not immediately dangerous, but worth monitoring and potentially addressing.
    /// </summary>
    private async Task DetectResourceTrendsAsync(StreamingResourceSession session)
    {
        // Track memory efficiency (memory growth per item processed)
        if (session.ItemsProcessed > 1000) // Only analyze after processing significant items
        {
            var memoryPerItem = session.TotalMemoryGrowth / session.ItemsProcessed;
            
            // If each item is consuming more than 1KB of memory growth, that's concerning
            if (memoryPerItem > 1024) // 1KB per item
            {
                _logger.LogWarning("High memory consumption per item detected in streaming session {SessionId}: " +
                                  "{MemoryPerItemKB}KB per item. This may indicate inefficient processing or memory leaks",
                    session.SessionId, memoryPerItem / 1024);
                
                // Store this metric for trend analysis
                session.CustomMetrics["MemoryPerItem"] = memoryPerItem;
            }
        }
        
        // Track garbage collection frequency - high GC activity indicates memory pressure
        var currentGcCount = GetTotalGcCount();
        if (session.LastGcCount.HasValue)
        {
            var gcIncrease = currentGcCount - session.LastGcCount.Value;
            var timeWindow = DateTime.UtcNow - session.LastGcCheckTime;
            
            if (timeWindow.TotalMinutes >= 1 && gcIncrease > 10) // More than 10 GCs per minute
            {
                _logger.LogWarning("High garbage collection activity detected in streaming session {SessionId}: " +
                                  "{GcCount} collections in {Minutes:F1} minutes. This indicates memory pressure",
                    session.SessionId, gcIncrease, timeWindow.TotalMinutes);
                
                session.CustomMetrics["HighGcActivity"] = gcIncrease;
            }
        }
        
        session.LastGcCount = currentGcCount;
        session.LastGcCheckTime = DateTime.UtcNow;
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Handles detected resource pressure using streaming-optimized strategies.
    /// This is like performing emergency maintenance on a running machine - we need to fix the problem
    /// without stopping the operation if possible.
    /// </summary>
    private async Task HandleStreamingResourcePressureAsync(StreamingResourceSession session)
    {
        var pressureContext = new ResourcePressureContext
        {
            CurrentMemoryUsage = session.CurrentMemoryUsage,
            BaselineMemoryUsage = session.StartMemoryUsage,
            MemoryGrowthRate = session.MemoryGrowthRate,
            GarbageCollectionCount = GetTotalGcCount(),
            CurrentRequestType = session.RequestType,
            PressureDetectedAt = DateTime.UtcNow
        };
        
        // Add streaming-specific context
        pressureContext.Properties["SessionId"] = session.SessionId;
        pressureContext.Properties["ItemsProcessed"] = session.ItemsProcessed;
        pressureContext.Properties["StreamDuration"] = DateTime.UtcNow - session.StartTime;
        pressureContext.Properties["ItemsPerSecond"] = session.ItemsProcessed / (DateTime.UtcNow - session.StartTime).TotalSeconds;
        
        _logger.LogInformation("Applying streaming resource management for session {SessionId} using {Strategy} strategy",
            session.SessionId, _options.CleanupStrategy);
        
        // Apply appropriate cleanup strategy
        switch (_options.CleanupStrategy)
        {
            case ResourceCleanupStrategy.Conservative:
                await ApplyStreamingConservativeCleanupAsync(session, pressureContext);
                break;
                
            case ResourceCleanupStrategy.Balanced:
                await ApplyStreamingBalancedCleanupAsync(session, pressureContext);
                break;
                
            case ResourceCleanupStrategy.Aggressive:
                await ApplyStreamingAggressiveCleanupAsync(session, pressureContext);
                break;
        }
        
        // Execute custom cleanup if provided
        if (_options.CustomCleanupAction != null)
        {
            try
            {
                _options.CustomCleanupAction(pressureContext);
                _logger.LogDebug("Custom streaming cleanup action executed for session {SessionId}", session.SessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Custom streaming cleanup action failed for session {SessionId}", session.SessionId);
            }
        }
        
        // Measure cleanup effectiveness
        await Task.Delay(200); // Give cleanup time to take effect
        
        var memoryAfterCleanup = GC.GetTotalMemory(false);
        var memoryReclaimed = session.CurrentMemoryUsage - memoryAfterCleanup;
        session.CurrentMemoryUsage = memoryAfterCleanup;
        
        _logger.LogInformation("Streaming resource cleanup completed for session {SessionId}. " +
                              "Memory reclaimed: {ReclaimedMB}MB, Continuing with {ItemCount} items processed",
            session.SessionId, memoryReclaimed / 1_000_000, session.ItemsProcessed);
    }
    
    /// <summary>
    /// Conservative cleanup optimized for streaming operations.
    /// This approach prioritizes stream continuity over aggressive memory reclamation.
    /// </summary>
    private async Task ApplyStreamingConservativeCleanupAsync(StreamingResourceSession session, ResourcePressureContext context)
    {
        if (_options.AutoTriggerGarbageCollection)
        {
            // Only collect generation 0 to minimize disruption to the stream
            GC.Collect(0, GCCollectionMode.Optimized);
            await Task.Delay(25); // Minimal delay for streaming
        }
        
        // Clear any finalizable objects without forcing full GC
        GC.WaitForPendingFinalizers();
        
        _logger.LogDebug("Conservative streaming cleanup applied for session {SessionId} - minimal GC impact", session.SessionId);
    }
    
    /// <summary>
    /// Balanced cleanup for streaming that provides good memory reclamation with acceptable latency impact.
    /// </summary>
    private async Task ApplyStreamingBalancedCleanupAsync(StreamingResourceSession session, ResourcePressureContext context)
    {
        // First apply conservative cleanup
        await ApplyStreamingConservativeCleanupAsync(session, context);
        
        if (_options.AutoTriggerGarbageCollection)
        {
            // Collect generations 0 and 1, which are typically faster
            GC.Collect(1, GCCollectionMode.Optimized);
            await Task.Delay(50);
            
            // Consider compacting LOH if configured and we've been running for a while
            var streamDuration = DateTime.UtcNow - session.StartTime;
            if (_options.ForceFullGarbageCollection && streamDuration.TotalMinutes > 10)
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            }
        }
        
        _logger.LogDebug("Balanced streaming cleanup applied for session {SessionId} - gen 0-1 GC", session.SessionId);
    }
    
    /// <summary>
    /// Aggressive cleanup for streaming when memory pressure is critical.
    /// This prioritizes memory reclamation and accepts higher latency impact.
    /// </summary>
    private async Task ApplyStreamingAggressiveCleanupAsync(StreamingResourceSession session, ResourcePressureContext context)
    {
        // Apply balanced cleanup first
        await ApplyStreamingBalancedCleanupAsync(session, context);
        
        if (_options.AutoTriggerGarbageCollection)
        {
            // Full GC across all generations - this will cause a longer pause
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(); // Second pass to clean up objects freed by finalizers
            
            await Task.Delay(100); // Allow time for aggressive cleanup
        }
        
        _logger.LogWarning("Aggressive streaming cleanup applied for session {SessionId} - full GC forced. " +
                          "Stream may experience temporary latency increase", session.SessionId);
    }
    
    /// <summary>
    /// Performs final resource assessment when the stream completes.
    /// This provides a comprehensive summary of resource usage throughout the stream's lifetime.
    /// </summary>
    private async Task PerformFinalResourceAssessmentAsync(StreamingResourceSession session)
    {
        var finalMemory = GC.GetTotalMemory(false);
        var totalMemoryGrowth = finalMemory - session.StartMemoryUsage;
        var totalDuration = DateTime.UtcNow - session.StartTime;
        var averageItemsPerSecond = totalDuration.TotalSeconds > 0 ? session.ItemsProcessed / totalDuration.TotalSeconds : 0;
        var averageMemoryPerItem = session.ItemsProcessed > 0 ? totalMemoryGrowth / session.ItemsProcessed : 0;
        
        var finalAssessment = new
        {
            SessionId = session.SessionId,
            RequestType = session.RequestType.Name,
            TotalDurationMinutes = totalDuration.TotalMinutes,
            ItemsProcessed = session.ItemsProcessed,
            AverageItemsPerSecond = averageItemsPerSecond,
            StartMemoryMB = session.StartMemoryUsage / 1_000_000,
            FinalMemoryMB = finalMemory / 1_000_000,
            TotalMemoryGrowthMB = totalMemoryGrowth / 1_000_000,
            AverageMemoryPerItemBytes = averageMemoryPerItem,
            ResourceEfficiencyScore = CalculateResourceEfficiencyScore(session, totalMemoryGrowth, totalDuration)
        };
        
        _logger.LogInformation("Final streaming resource assessment: {@FinalAssessment}", finalAssessment);
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Calculates a resource efficiency score for the streaming operation.
    /// This provides a single metric to understand how efficiently resources were used.
    /// Score ranges from 0 (very inefficient) to 100 (highly efficient).
    /// </summary>
    private double CalculateResourceEfficiencyScore(StreamingResourceSession session, long totalMemoryGrowth, TimeSpan totalDuration)
    {
        var score = 100.0; // Start with perfect score
        
        // Penalize high memory growth per item
        if (session.ItemsProcessed > 0)
        {
            var memoryPerItem = totalMemoryGrowth / session.ItemsProcessed;
            if (memoryPerItem > 1024) // More than 1KB per item
            {
                score -= Math.Min(30, memoryPerItem / 1024 * 5); // Up to 30 point penalty
            }
        }
        
        // Penalize high total memory growth
        var memoryGrowthMB = totalMemoryGrowth / 1_000_000.0;
        if (memoryGrowthMB > 100) // More than 100MB growth
        {
            score -= Math.Min(25, (memoryGrowthMB - 100) / 10); // Up to 25 point penalty
        }
        
        // Penalize low throughput (less than 1 item per second)
        var itemsPerSecond = totalDuration.TotalSeconds > 0 ? session.ItemsProcessed / totalDuration.TotalSeconds : 0;
        if (itemsPerSecond < 1.0)
        {
            score -= Math.Min(20, (1.0 - itemsPerSecond) * 20); // Up to 20 point penalty
        }
        
        return Math.Max(0, score); // Ensure score doesn't go below 0
    }
    
    /// <summary>
    /// Performs resource cleanup when the stream ends (normally or abnormally).
    /// </summary>
    private async Task PerformResourceCleanupAsync(StreamingResourceSession session, string reason)
    {
        _logger.LogDebug("Performing resource cleanup for streaming session {SessionId}. Reason: {Reason}", 
            session.SessionId, reason);
        
        // Apply conservative cleanup to free any accumulated resources
        if (_options.AutoTriggerGarbageCollection)
        {
            GC.Collect(0);
            await Task.Delay(50);
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Logs detailed memory statistics specific to streaming operations.
    /// </summary>
    private async Task LogDetailedStreamingMemoryStatsAsync(StreamingResourceSession session)
    {
        var detailedStats = new
        {
            SessionId = session.SessionId,
            StreamingSpecificMetrics = new
            {
                ItemsProcessed = session.ItemsProcessed,
                MemoryPerItem = session.ItemsProcessed > 0 ? session.TotalMemoryGrowth / session.ItemsProcessed : 0,
                ItemsPerSecond = (DateTime.UtcNow - session.StartTime).TotalSeconds > 0 ? 
                    session.ItemsProcessed / (DateTime.UtcNow - session.StartTime).TotalSeconds : 0,
                StreamDurationMinutes = (DateTime.UtcNow - session.StartTime).TotalMinutes
            },
            StandardMemoryMetrics = new
            {
                CurrentMemoryMB = GC.GetTotalMemory(false) / 1_000_000,
                MemoryGrowthMB = session.TotalMemoryGrowth / 1_000_000,
                MemoryGrowthRateMBPerSec = session.MemoryGrowthRate / 1_000_000,
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2),
                WorkingSetMB = Environment.WorkingSet / 1_000_000
            },
            CustomMetrics = session.CustomMetrics
        };
        
        _logger.LogDebug("Detailed streaming memory stats: {@DetailedStats}", detailedStats);
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Gets the total garbage collection count across all generations.
    /// </summary>
    private static int GetTotalGcCount()
    {
        return GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
    }
}

/// <summary>
/// Represents a resource monitoring session for a streaming operation.
/// This tracks all metrics and state needed for resource management throughout a stream's lifecycle.
/// </summary>
internal class StreamingResourceSession
{
    public string SessionId { get; set; } = string.Empty;
    public Type RequestType { get; set; } = typeof(object);
    public DateTime StartTime { get; set; }
    public long StartMemoryUsage { get; set; }
    public long CurrentMemoryUsage { get; set; }
    public long TotalMemoryGrowth { get; set; }
    public double MemoryGrowthRate { get; set; }
    public long ItemsProcessed { get; set; }
    public DateTime LastResourceCheck { get; set; }
    public int? LastGcCount { get; set; }
    public DateTime LastGcCheckTime { get; set; }
    public Dictionary<string, object> CustomMetrics { get; set; } = new();
}