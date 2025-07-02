using System.Runtime.CompilerServices;
using System.Threading.Channels;
using FS.Mediator.Features.HealthChecking.Models;
using FS.Mediator.Features.HealthChecking.Models.Enums;
using FS.Mediator.Features.HealthChecking.Models.Options;
using FS.Mediator.Features.HealthChecking.Services;
using FS.Mediator.Features.StreamHandling.Core;
using Microsoft.Extensions.Logging;

namespace FS.Mediator.Features.HealthChecking.Behaviors.Streaming.Diagnostics;

/// <summary>
/// Comprehensive health check and diagnostics behavior for streaming operations.
/// 
/// This behavior implements a sophisticated health monitoring system that tracks
/// multiple aspects of stream performance and health. It's designed to be non-intrusive
/// while providing deep insights into stream behavior.
/// 
/// Key capabilities:
/// - Real-time performance monitoring (throughput, latency)
/// - Resource usage tracking (memory, GC pressure)
/// - Health status assessment with configurable thresholds
/// - Integration with monitoring systems through IStreamHealthReporter
/// - Detailed diagnostic information collection
/// 
/// The behavior uses the channel-based pattern to ensure all health checking
/// logic is separated from the yielding operations, preventing any interference
/// with the stream's performance.
/// </summary>
/// <typeparam name="TRequest">The type of streaming request</typeparam>
/// <typeparam name="TResponse">The type of each item in the stream</typeparam>
public class HealthCheckBehavior<TRequest, TResponse>(
    ILogger<HealthCheckBehavior<TRequest, TResponse>> logger,
    HealthCheckBehaviorOptions options,
    IStreamHealthReporter healthReporter)
    : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public async IAsyncEnumerable<TResponse> HandleAsync(
        TRequest request, 
        StreamRequestHandlerDelegate<TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Initialize health metrics for this stream instance
        var healthMetrics = new StreamHealthMetrics
        {
            RequestTypeName = typeof(TRequest).Name
        };
        
        logger.LogInformation("Starting health monitoring for stream {RequestType} with correlation {CorrelationId}", 
            healthMetrics.RequestTypeName, healthMetrics.CorrelationId);

        // Create channel for separating health monitoring from yielding
        var channel = Channel.CreateUnbounded<TResponse>();
        var reader = channel.Reader;
        var writer = channel.Writer;

        // Health monitoring task runs in background with full error handling capability
        var monitoringTask = Task.Run(async () =>
        {
            var lastHealthCheck = DateTime.UtcNow;
            
            try
            {
                // Process the stream while continuously monitoring health
                await foreach (var item in next(cancellationToken))
                {
                    // Record that we successfully processed an item
                    healthMetrics.RecordItemProcessed();
                    
                    // Perform periodic health assessments
                    var now = DateTime.UtcNow;
                    if ((now - lastHealthCheck).TotalSeconds >= options.HealthCheckIntervalSeconds)
                    {
                        await PerformHealthCheckAsync(healthMetrics, cancellationToken);
                        lastHealthCheck = now;
                    }
                    
                    // Handle memory pressure if configured
                    if (options.AutoTriggerGarbageCollection)
                    {
                        await HandleMemoryPressureAsync(healthMetrics);
                    }
                    
                    // Write item to channel for consumption
                    await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
                }
                
                // Stream completed successfully - perform final health assessment
                healthMetrics.HealthStatus = StreamHealthStatus.Healthy;
                await PerformFinalHealthReportAsync(healthMetrics, cancellationToken);
                
                logger.LogInformation("Health monitoring completed for stream {CorrelationId}. Final status: {HealthStatus}, Items processed: {ItemCount}",
                    healthMetrics.CorrelationId, healthMetrics.HealthStatus, healthMetrics.TotalItems);
            }
            catch (OperationCanceledException)
            {
                // Stream was cancelled - this is normal, not a health issue
                healthMetrics.HealthStatus = StreamHealthStatus.Healthy;
                logger.LogInformation("Stream {CorrelationId} was cancelled after processing {ItemCount} items",
                    healthMetrics.CorrelationId, healthMetrics.TotalItems);
            }
            catch (Exception ex)
            {
                // Stream failed - record the error and update health status
                healthMetrics.RecordError(ex);
                healthMetrics.HealthStatus = StreamHealthStatus.Failed;
                
                // Report critical issue
                var criticalWarning = new HealthWarning
                {
                    Timestamp = DateTime.UtcNow,
                    Type = HealthWarningType.ErrorOccurred,
                    Message = "Stream processing failed with unhandled exception",
                    Details = ex.ToString(),
                    Recommendation = "Review error details and consider implementing retry logic or error handling"
                };
                
                await healthReporter.ReportCriticalIssueAsync(healthMetrics, criticalWarning, cancellationToken);
                
                logger.LogError(ex, "Health monitoring detected stream failure for {CorrelationId} after processing {ItemCount} items",
                    healthMetrics.CorrelationId, healthMetrics.TotalItems);
                
                throw; // Re-throw to maintain error semantics
            }
            finally
            {
                // Always close the channel to signal completion
                writer.Complete();
            }
        }, cancellationToken);

        // Yield items from channel - this is completely safe and has no error handling
        // The health monitoring happens entirely in the background task
        await foreach (var item in reader.ReadAllAsync(cancellationToken))
        {
            yield return item; // ← Safe yielding with comprehensive health monitoring in background
        }

        // Ensure monitoring task completed and handle any errors
        await monitoringTask;
    }
    
    /// <summary>
    /// Performs a comprehensive health assessment of the current stream.
    /// This method evaluates all health metrics and triggers appropriate actions
    /// based on the findings.
    /// </summary>
    private async Task PerformHealthCheckAsync(StreamHealthMetrics metrics, CancellationToken cancellationToken)
    {
        // Update health status based on current metrics
        metrics.AssessHealthStatus();
        
        // Report health status to monitoring system
        await healthReporter.ReportHealthAsync(metrics, cancellationToken);
        
        // Check for critical issues that require immediate attention
        var recentWarnings = metrics.HealthWarnings
            .Where(w => w.Timestamp > DateTime.UtcNow.AddSeconds(-options.HealthCheckIntervalSeconds * 2))
            .ToList();
        
        // Report critical warnings
        foreach (var warning in recentWarnings)
        {
            if (IsCriticalWarning(warning))
            {
                await healthReporter.ReportCriticalIssueAsync(metrics, warning, cancellationToken);
            }
        }
        
        // Log detailed health information if configured
        if (options.IncludeDetailedMemoryStats)
        {
            await LogDetailedMemoryStatsAsync(metrics);
        }
    }
    
    /// <summary>
    /// Handles memory pressure situations by triggering garbage collection if needed.
    /// This is an optional feature that can help with memory-intensive streaming operations.
    /// </summary>
    private async Task HandleMemoryPressureAsync(StreamHealthMetrics metrics)
    {
        var memoryGrowth = metrics.CurrentMemoryUsage - metrics.StartMemoryUsage;
        
        if (memoryGrowth > options.MemoryGrowthThresholdBytes)
        {
            logger.LogWarning("High memory usage detected for stream {CorrelationId}: {MemoryGrowthMB}MB growth. Triggering garbage collection",
                metrics.CorrelationId, memoryGrowth / 1_000_000);
            
            // Force garbage collection to reclaim memory
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            // Give the system a moment to complete GC
            await Task.Delay(100);
            
            // Log the results
            var newMemoryUsage = GC.GetTotalMemory(false);
            var memoryReclaimed = metrics.CurrentMemoryUsage - newMemoryUsage;
            
            logger.LogInformation("Garbage collection completed for stream {CorrelationId}. Memory reclaimed: {MemoryReclaimedMB}MB",
                metrics.CorrelationId, memoryReclaimed / 1_000_000);
        }
    }
    
    /// <summary>
    /// Performs final health reporting when the stream completes.
    /// This provides a comprehensive summary of the stream's health throughout its lifetime.
    /// </summary>
    private async Task PerformFinalHealthReportAsync(StreamHealthMetrics metrics, CancellationToken cancellationToken)
    {
        // Perform final health assessment
        metrics.AssessHealthStatus();
        
        // Calculate final statistics
        var totalDuration = DateTime.UtcNow - metrics.StreamStartTime;
        var averageThroughput = metrics.TotalItems / totalDuration.TotalSeconds;
        
        // Create comprehensive final report
        var finalReport = new
        {
            CorrelationId = metrics.CorrelationId,
            RequestType = metrics.RequestTypeName,
            FinalHealthStatus = metrics.HealthStatus.ToString(),
            
            // Performance statistics
            TotalItems = metrics.TotalItems,
            TotalDurationSeconds = totalDuration.TotalSeconds,
            AverageThroughput = averageThroughput,
            PeakThroughput = metrics.PeakThroughput,
            
            // Resource usage
            TotalMemoryGrowthMB = (metrics.CurrentMemoryUsage - metrics.StartMemoryUsage) / 1_000_000,
            PeakMemoryUsageMB = metrics.PeakMemoryUsage / 1_000_000,
            GarbageCollectionCount = metrics.GarbageCollectionCount,
            
            // Health indicators
            TotalWarnings = metrics.HealthWarnings.Count,
            TotalErrors = metrics.ErrorCount,
            ErrorRate = metrics.TotalItems > 0 ? (double)metrics.ErrorCount / metrics.TotalItems : 0.0
        };
        
        logger.LogInformation("Final health report for stream {CorrelationId}: {@FinalReport}", 
            metrics.CorrelationId, finalReport);
        
        // Send final health report to monitoring system
        await healthReporter.ReportHealthAsync(metrics, cancellationToken);
    }
    
    /// <summary>
    /// Determines whether a health warning is critical and requires immediate attention.
    /// </summary>
    private bool IsCriticalWarning(HealthWarning warning)
    {
        return warning.Type switch
        {
            HealthWarningType.HighErrorRate => true,
            HealthWarningType.StreamStalled => true,
            HealthWarningType.ResourceExhaustion => true,
            _ => false
        };
    }
    
    /// <summary>
    /// Logs detailed memory statistics for troubleshooting purposes.
    /// This provides deep insights into memory usage patterns.
    /// </summary>
    private Task LogDetailedMemoryStatsAsync(StreamHealthMetrics metrics)
    {
        var memoryInfo = new
        {
            TotalMemoryMB = GC.GetTotalMemory(false) / 1_000_000,
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            TotalAvailableMemoryMB = GC.GetTotalMemory(false) / 1_000_000,
            WorkingSetMB = Environment.WorkingSet / 1_000_000
        };
        
        logger.LogDebug("Detailed memory statistics for stream {CorrelationId}: {@MemoryInfo}", 
            metrics.CorrelationId, memoryInfo);
        
        return Task.CompletedTask;
    }
}