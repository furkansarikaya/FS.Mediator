using System.Collections.Concurrent;
using FS.Mediator.Features.HealthChecking.Models.Enums;

namespace FS.Mediator.Features.HealthChecking.Models;

/// <summary>
/// Comprehensive health metrics for streaming operations.
/// 
/// This class captures the essential health indicators that help us understand
/// whether a streaming operation is functioning properly. Think of it as the
/// "vital signs" of your stream - like checking pulse, blood pressure, and
/// temperature for a patient.
/// 
/// The metrics are designed to be collected in real-time without impacting
/// performance, and they provide actionable insights for troubleshooting.
/// </summary>
public class StreamHealthMetrics
{
    // === Timing and Performance Metrics ===
    
    /// <summary>
    /// When the stream started processing. This is our baseline for all time-based calculations.
    /// </summary>
    public DateTime StreamStartTime { get; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the first item was produced by the stream. The difference between StreamStartTime
    /// and this value tells us about "startup latency" - how long it takes for the stream
    /// to actually start producing data.
    /// </summary>
    public DateTime? FirstItemTime { get; set; }
    
    /// <summary>
    /// When the most recent item was produced. This helps us detect "stalls" - situations
    /// where the stream was producing data but then stopped for an unusually long time.
    /// </summary>
    public DateTime LastItemTime { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Total number of items successfully processed by the stream.
    /// This is our primary throughput indicator.
    /// </summary>
    public long TotalItems { get; set; }
    
    /// <summary>
    /// Current rate of item processing (items per second).
    /// This is calculated as a rolling average to smooth out short-term fluctuations.
    /// </summary>
    public double CurrentThroughput => CalculateCurrentThroughput();
    
    /// <summary>
    /// Peak throughput achieved during this stream's lifetime.
    /// This helps us understand the stream's best-case performance.
    /// </summary>
    public double PeakThroughput { get; set; }
    
    // === Resource Usage Metrics ===
    
    /// <summary>
    /// Memory usage when the stream started (in bytes).
    /// This baseline helps us detect memory leaks in long-running streams.
    /// </summary>
    public long StartMemoryUsage { get; } = GC.GetTotalMemory(false);
    
    /// <summary>
    /// Current memory usage (in bytes).
    /// Significant growth from StartMemoryUsage might indicate a memory leak.
    /// </summary>
    public long CurrentMemoryUsage => GC.GetTotalMemory(false);
    
    /// <summary>
    /// Maximum memory usage observed during stream processing.
    /// This helps us understand the memory footprint of our streaming operations.
    /// </summary>
    public long PeakMemoryUsage { get; set; }
    
    /// <summary>
    /// Number of garbage collections that occurred during stream processing.
    /// Frequent GC can indicate memory pressure and performance issues.
    /// </summary>
    public int GarbageCollectionCount { get; set; }
    
    // === Health Status Indicators ===
    
    /// <summary>
    /// Overall health status of the stream based on various indicators.
    /// This provides a quick "traffic light" view of stream health.
    /// </summary>
    public StreamHealthStatus HealthStatus { get; set; } = StreamHealthStatus.Healthy;
    
    /// <summary>
    /// Collection of health warnings that have been detected.
    /// Each warning includes details about what was detected and when.
    /// </summary>
    public ConcurrentBag<HealthWarning> HealthWarnings { get; } = new();
    
    /// <summary>
    /// Number of errors encountered during stream processing.
    /// This includes both recoverable and non-recoverable errors.
    /// </summary>
    public long ErrorCount { get; set; }
    
    /// <summary>
    /// Time since the last item was produced. This helps detect stalled streams.
    /// A high value might indicate the stream has stopped producing data.
    /// </summary>
    public TimeSpan TimeSinceLastItem => DateTime.UtcNow - LastItemTime;
    
    // === Diagnostic Properties ===
    
    /// <summary>
    /// Unique identifier for correlating this stream across different monitoring systems.
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    
    /// <summary>
    /// Type name of the request being processed. This helps group metrics by operation type.
    /// </summary>
    public string RequestTypeName { get; set; } = string.Empty;
    
    /// <summary>
    /// Custom properties that can be added by specific behaviors or handlers.
    /// This provides extensibility for domain-specific metrics.
    /// </summary>
    public ConcurrentDictionary<string, object> CustomProperties { get; } = new();
    
    // === Health Assessment Methods ===
    
    /// <summary>
    /// Records that a new item was successfully processed.
    /// This method updates all relevant metrics and performs health assessments.
    /// </summary>
    public void RecordItemProcessed()
    {
        var now = DateTime.UtcNow;
        
        // Update basic counters
        TotalItems++;
        LastItemTime = now;
        
        // Record first item time if this is the first item
        FirstItemTime ??= now;
        
        // Update peak throughput tracking
        var currentRate = CurrentThroughput;
        if (currentRate > PeakThroughput)
        {
            PeakThroughput = currentRate;
        }
        
        // Update memory tracking
        var currentMemory = CurrentMemoryUsage;
        if (currentMemory > PeakMemoryUsage)
        {
            PeakMemoryUsage = currentMemory;
        }
        
        // Check for garbage collection activity
        var currentGcCount = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
        GarbageCollectionCount = currentGcCount;
    }
    
    /// <summary>
    /// Records that an error occurred during stream processing.
    /// This method also triggers health status reassessment.
    /// </summary>
    public void RecordError(Exception exception)
    {
        ErrorCount++;
        
        // Add health warning for the error
        var warning = new HealthWarning
        {
            Timestamp = DateTime.UtcNow,
            Type = HealthWarningType.ErrorOccurred,
            Message = $"Error during stream processing: {exception.GetType().Name}",
            Details = exception.Message
        };
        
        HealthWarnings.Add(warning);
        
        // Reassess health status
        AssessHealthStatus();
    }
    
    /// <summary>
    /// Evaluates the current health status based on various metrics.
    /// This is the "brain" of our health checking system - it looks at all
    /// the metrics we've collected and makes an overall health determination.
    /// </summary>
    public void AssessHealthStatus()
    {
        var warnings = new List<HealthWarning>();
        
        // Check for stalled stream (no items for a while)
        if (TotalItems > 0 && TimeSinceLastItem.TotalSeconds > 30)
        {
            warnings.Add(new HealthWarning
            {
                Type = HealthWarningType.StreamStalled,
                Message = $"No items processed for {TimeSinceLastItem.TotalSeconds:F1} seconds",
                Timestamp = DateTime.UtcNow
            });
        }
        
        // Check for memory growth (potential memory leak)
        var memoryGrowth = CurrentMemoryUsage - StartMemoryUsage;
        if (memoryGrowth > 100_000_000) // 100MB growth
        {
            warnings.Add(new HealthWarning
            {
                Type = HealthWarningType.HighMemoryUsage,
                Message = $"Memory usage increased by {memoryGrowth / 1_000_000}MB since stream start",
                Timestamp = DateTime.UtcNow
            });
        }
        
        // Check for low throughput
        if (TotalItems > 100 && CurrentThroughput < 1.0) // Less than 1 item per second after 100 items
        {
            warnings.Add(new HealthWarning
            {
                Type = HealthWarningType.LowThroughput,
                Message = $"Low throughput detected: {CurrentThroughput:F2} items/second",
                Timestamp = DateTime.UtcNow
            });
        }
        
        // Check error rate
        if (TotalItems > 0)
        {
            var errorRate = (double)ErrorCount / TotalItems;
            if (errorRate > 0.1) // More than 10% error rate
            {
                warnings.Add(new HealthWarning
                {
                    Type = HealthWarningType.HighErrorRate,
                    Message = $"High error rate detected: {errorRate:P1}",
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        
        // Add new warnings to the collection
        foreach (var warning in warnings)
        {
            HealthWarnings.Add(warning);
        }
        
        // Determine overall health status
        HealthStatus = warnings.Count switch
        {
            0 => StreamHealthStatus.Healthy,
            <= 2 => StreamHealthStatus.Warning,
            _ => StreamHealthStatus.Unhealthy
        };
    }
    
    /// <summary>
    /// Calculates the current throughput as a rolling average.
    /// This provides a smoothed view of performance that isn't affected by
    /// short-term fluctuations in processing speed.
    /// </summary>
    private double CalculateCurrentThroughput()
    {
        if (TotalItems == 0 || !FirstItemTime.HasValue)
            return 0.0;
        
        var elapsed = DateTime.UtcNow - FirstItemTime.Value;
        if (elapsed.TotalSeconds < 1)
            return 0.0;
        
        return TotalItems / elapsed.TotalSeconds;
    }
}