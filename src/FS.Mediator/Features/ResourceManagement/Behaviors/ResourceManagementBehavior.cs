using System.Collections.Concurrent;
using System.Runtime;
using FS.Mediator.Features.RequestHandling.Core;
using FS.Mediator.Features.ResourceManagement.Models;
using FS.Mediator.Features.ResourceManagement.Models.Enums;
using FS.Mediator.Features.ResourceManagement.Models.Options;
using Microsoft.Extensions.Logging;

namespace FS.Mediator.Features.ResourceManagement.Behaviors;

/// <summary>
/// Pipeline behavior that manages system resources during request processing.
/// 
/// Think of this behavior as your application's "resource guardian" - it continuously
/// monitors memory usage, tracks disposable resources, and takes corrective action
/// when resource pressure builds up. This is particularly crucial in long-running
/// applications that process many requests over time.
/// 
/// The behavior operates on several levels:
/// 1. **Preventive Monitoring**: Continuously tracks resource usage patterns
/// 2. **Early Warning**: Detects concerning trends before they become problems  
/// 3. **Active Management**: Takes corrective action when thresholds are exceeded
/// 4. **Resource Tracking**: Ensures proper cleanup of disposable resources
/// 
/// This is especially valuable in microservice architectures where memory leaks
/// can cause cascading failures across the entire system.
/// </summary>
/// <typeparam name="TRequest">The type of request being processed</typeparam>
/// <typeparam name="TResponse">The type of response returned by the request</typeparam>
public class ResourceManagementBehavior<TRequest, TResponse>(
    ILogger<ResourceManagementBehavior<TRequest, TResponse>> logger,
    ResourceManagementOptions options) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<ResourceManagementBehavior<TRequest, TResponse>> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ResourceManagementOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    
    // Static monitoring state shared across all instances to track global resource usage
    private static readonly ConcurrentDictionary<string, ResourceMonitoringSession> ActiveSessions = new();
    private static readonly object GlobalMonitoringLock = new();
    private static DateTime LastGlobalCleanup = DateTime.UtcNow;
    
    /// <inheritdoc />
    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
    {
        var requestName = typeof(TRequest).Name;
        var sessionId = Guid.NewGuid().ToString("N")[..8];
        
        // Create a monitoring session for this specific request
        var session = new ResourceMonitoringSession
        {
            SessionId = sessionId,
            RequestType = typeof(TRequest),
            StartTime = DateTime.UtcNow,
            StartMemoryUsage = GC.GetTotalMemory(false),
            TrackedDisposables = _options.EnableDisposableResourceTracking 
                ? new ConcurrentBag<WeakReference>() 
                : null
        };
        
        ActiveSessions[sessionId] = session;
        
        _logger.LogDebug("Starting resource monitoring for {RequestName} with session {SessionId}. Initial memory: {MemoryMB}MB",
            requestName, sessionId, session.StartMemoryUsage / 1_000_000);
        
        try
        {
            // Monitor resources before request execution
            await PerformResourceCheckAsync(session, "Pre-execution");
            
            // Execute the request with resource tracking
            var response = await next(cancellationToken);
            
            // Monitor resources after successful execution
            await PerformResourceCheckAsync(session, "Post-execution");
            
            return response;
        }
        catch (Exception)
        {
            // Even on failure, we want to check for resource issues that might have contributed
            await PerformResourceCheckAsync(session, "Post-exception");
            
            _logger.LogWarning("Request {RequestName} failed in session {SessionId}. Checking if resource pressure contributed to failure",
                requestName, sessionId);
            
            throw;
        }
        finally
        {
            // Always clean up the monitoring session and any tracked resources
            await CleanupSessionAsync(session);
            ActiveSessions.TryRemove(sessionId, out _);
        }
    }
    
    /// <summary>
    /// Performs a comprehensive resource check and takes action if thresholds are exceeded.
    /// This is the heart of our resource management logic.
    /// </summary>
    private async Task PerformResourceCheckAsync(ResourceMonitoringSession session, string checkpointName)
    {
        var currentMemory = GC.GetTotalMemory(false);
        var memoryGrowth = currentMemory - session.StartMemoryUsage;
        var timeElapsed = DateTime.UtcNow - session.StartTime;
        var growthRate = timeElapsed.TotalSeconds > 0 ? memoryGrowth / timeElapsed.TotalSeconds : 0;
        
        // Update session metrics
        session.CurrentMemoryUsage = currentMemory;
        session.MemoryGrowthRate = growthRate;
        session.LastCheckpoint = checkpointName;
        
        // Log detailed diagnostics if enabled
        if (_options.CollectDetailedMemoryStats)
        {
            await LogDetailedMemoryStatsAsync(session);
        }
        
        // Check if we've exceeded memory thresholds
        var exceedsMemoryThreshold = currentMemory > _options.MaxMemoryThresholdBytes;
        var exceedsGrowthRateThreshold = growthRate > _options.MemoryGrowthRateThresholdBytesPerSecond;
        
        if (exceedsMemoryThreshold || exceedsGrowthRateThreshold)
        {
            _logger.LogWarning("Resource pressure detected at {Checkpoint} for session {SessionId}: Memory={MemoryMB}MB, Growth={GrowthMB}MB, Rate={RateMB}MB/s",
                checkpointName, session.SessionId, currentMemory / 1_000_000, memoryGrowth / 1_000_000, growthRate / 1_000_000);
            
            await HandleResourcePressureAsync(session);
        }
        
        // Perform global cleanup if enough time has passed
        await PerformGlobalMaintenanceIfNeededAsync();
    }
    
    /// <summary>
    /// Handles detected resource pressure by applying the configured cleanup strategy.
    /// This method escalates through different levels of cleanup based on severity.
    /// </summary>
    private async Task HandleResourcePressureAsync(ResourceMonitoringSession session)
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
        
        // Add session-specific properties to the context
        pressureContext.Properties["SessionId"] = session.SessionId;
        pressureContext.Properties["Checkpoint"] = session.LastCheckpoint;
        pressureContext.Properties["RequestDuration"] = DateTime.UtcNow - session.StartTime;
        
        _logger.LogInformation("Applying {Strategy} cleanup strategy for session {SessionId}",
            _options.CleanupStrategy, session.SessionId);
        
        // Apply cleanup strategy based on configuration
        switch (_options.CleanupStrategy)
        {
            case ResourceCleanupStrategy.Conservative:
                await ApplyConservativeCleanupAsync(pressureContext);
                break;
                
            case ResourceCleanupStrategy.Balanced:
                await ApplyBalancedCleanupAsync(pressureContext);
                break;
                
            case ResourceCleanupStrategy.Aggressive:
                await ApplyAggressiveCleanupAsync(pressureContext);
                break;
        }
        
        // Execute custom cleanup action if provided
        if (_options.CustomCleanupAction != null)
        {
            try
            {
                _options.CustomCleanupAction(pressureContext);
                _logger.LogDebug("Custom cleanup action executed successfully for session {SessionId}", session.SessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Custom cleanup action failed for session {SessionId}", session.SessionId);
            }
        }
        
        // Wait a moment for cleanup effects to take place, then log results
        await Task.Delay(100);
        
        var memoryAfterCleanup = GC.GetTotalMemory(false);
        var memoryReclaimed = session.CurrentMemoryUsage - memoryAfterCleanup;
        
        _logger.LogInformation("Resource cleanup completed for session {SessionId}. Memory reclaimed: {ReclaimedMB}MB",
            session.SessionId, memoryReclaimed / 1_000_000);
    }
    
    /// <summary>
    /// Applies conservative cleanup strategy - minimal impact, maximum safety.
    /// This is like "gentle housekeeping" - we clean up what we can without disrupting operations.
    /// </summary>
    private async Task ApplyConservativeCleanupAsync(ResourcePressureContext context)
    {
        // Suggest garbage collection if automatic triggering is enabled
        if (_options.AutoTriggerGarbageCollection)
        {
            GC.Collect(0); // Only collect generation 0 (young objects)
            await Task.Delay(50); // Give GC time to work
        }
        
        // Clean up any finalized objects
        GC.WaitForPendingFinalizers();
        
        _logger.LogDebug("Conservative cleanup applied - generation 0 GC and finalizer cleanup");
    }
    
    /// <summary>
    /// Applies balanced cleanup strategy - moderate impact, good effectiveness.
    /// This is like "thorough housekeeping" - we clean more aggressively but still maintain performance.
    /// </summary>
    private async Task ApplyBalancedCleanupAsync(ResourcePressureContext context)
    {
        // First apply conservative cleanup
        await ApplyConservativeCleanupAsync(context);
        
        if (_options.AutoTriggerGarbageCollection)
        {
            // Collect generations 0 and 1
            GC.Collect(1);
            await Task.Delay(100);
            
            // Try to compact the large object heap if possible
            if (_options.ForceFullGarbageCollection)
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            }
        }
        
        _logger.LogDebug("Balanced cleanup applied - generations 0-1 GC with LOH consideration");
    }
    
    /// <summary>
    /// Applies aggressive cleanup strategy - maximum impact, maximum effectiveness.
    /// This is like "deep cleaning" - we prioritize memory reclamation over performance.
    /// </summary>
    private async Task ApplyAggressiveCleanupAsync(ResourcePressureContext context)
    {
        // First apply balanced cleanup
        await ApplyBalancedCleanupAsync(context);
        
        if (_options.AutoTriggerGarbageCollection)
        {
            // Full garbage collection across all generations
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect(); // Second pass to clean up anything freed by finalizers
            
            await Task.Delay(200); // Give more time for aggressive cleanup
        }
        
        // Force cleanup of tracked disposable resources
        await CleanupAbandonedDisposablesAsync();
        
        _logger.LogDebug("Aggressive cleanup applied - full GC with disposable resource cleanup");
    }
    
    /// <summary>
    /// Cleans up disposable resources that may have been abandoned by request handlers.
    /// This helps prevent resource leaks from handlers that forget to dispose resources properly.
    /// </summary>
    private async Task CleanupAbandonedDisposablesAsync()
    {
        if (!_options.EnableDisposableResourceTracking) return;
        
        var cleanupCount = 0;
        
        // Check all active sessions for abandoned disposables
        foreach (var session in ActiveSessions.Values)
        {
            if (session.TrackedDisposables == null) continue;
            
            var disposablesToCleanup = new List<IDisposable>();
            
            // Collect disposables that are still alive but may be abandoned
            foreach (var weakRef in session.TrackedDisposables)
            {
                if (weakRef.Target is IDisposable disposable && weakRef.IsAlive)
                {
                    disposablesToCleanup.Add(disposable);
                }
            }
            
            // Dispose abandoned resources
            foreach (var disposable in disposablesToCleanup)
            {
                try
                {
                    disposable.Dispose();
                    cleanupCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to dispose abandoned resource of type {ResourceType} in session {SessionId}",
                        disposable.GetType().Name, session.SessionId);
                }
            }
        }
        
        if (cleanupCount > 0)
        {
            _logger.LogInformation("Cleaned up {CleanupCount} abandoned disposable resources", cleanupCount);
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Performs global maintenance tasks periodically.
    /// This ensures system-wide resource health even when individual requests aren't triggering cleanup.
    /// </summary>
    private async Task PerformGlobalMaintenanceIfNeededAsync()
    {
        // Check if enough time has passed since the last global cleanup
        if (DateTime.UtcNow - LastGlobalCleanup < TimeSpan.FromSeconds(_options.MonitoringIntervalSeconds))
        {
            return;
        }
        
        lock (GlobalMonitoringLock)
        {
            // Double-check inside the lock
            if (DateTime.UtcNow - LastGlobalCleanup < TimeSpan.FromSeconds(_options.MonitoringIntervalSeconds))
            {
                return;
            }
            
            LastGlobalCleanup = DateTime.UtcNow;
        }
        
        // Clean up completed sessions
        var completedSessions = ActiveSessions.Where(kvp => 
            DateTime.UtcNow - kvp.Value.StartTime > TimeSpan.FromMinutes(10)).ToList();
        
        foreach (var (sessionId, _) in completedSessions)
        {
            ActiveSessions.TryRemove(sessionId, out _);
        }
        
        // Log global resource statistics
        var totalMemory = GC.GetTotalMemory(false);
        var activeSessions = ActiveSessions.Count;
        
        _logger.LogDebug("Global maintenance performed: {TotalMemoryMB}MB total memory, {ActiveSessions} active sessions, {CompletedSessions} sessions cleaned up",
            totalMemory / 1_000_000, activeSessions, completedSessions.Count);
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Cleans up a specific monitoring session and its tracked resources.
    /// This ensures proper resource cleanup when request processing completes.
    /// </summary>
    private async Task CleanupSessionAsync(ResourceMonitoringSession session)
    {
        if (session.TrackedDisposables == null) return;
        
        var cleanupCount = 0;
        
        // Dispose any remaining tracked resources
        foreach (var weakRef in session.TrackedDisposables)
        {
            if (weakRef.Target is IDisposable disposable && weakRef.IsAlive)
            {
                try
                {
                    disposable.Dispose();
                    cleanupCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to dispose tracked resource of type {ResourceType} during session cleanup {SessionId}",
                        disposable.GetType().Name, session.SessionId);
                }
            }
        }
        
        if (cleanupCount > 0)
        {
            _logger.LogDebug("Session cleanup completed for {SessionId}: disposed {CleanupCount} tracked resources",
                session.SessionId, cleanupCount);
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Logs detailed memory statistics for diagnostic purposes.
    /// This provides deep insights into memory usage patterns for troubleshooting.
    /// </summary>
    private async Task LogDetailedMemoryStatsAsync(ResourceMonitoringSession session)
    {
        var memoryInfo = new
        {
            TotalMemoryMB = GC.GetTotalMemory(false) / 1_000_000,
            TotalMemoryWithGCMB = GC.GetTotalMemory(true) / 1_000_000,
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            WorkingSetMB = Environment.WorkingSet / 1_000_000,
            SessionMemoryGrowthMB = (session.CurrentMemoryUsage - session.StartMemoryUsage) / 1_000_000,
            SessionDurationSeconds = (DateTime.UtcNow - session.StartTime).TotalSeconds,
            ActiveSessionsCount = ActiveSessions.Count
        };
        
        _logger.LogDebug("Detailed memory stats for session {SessionId} at {Checkpoint}: {@MemoryInfo}",
            session.SessionId, session.LastCheckpoint, memoryInfo);
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Gets the total garbage collection count across all generations.
    /// This provides a single metric for GC activity.
    /// </summary>
    private static int GetTotalGcCount()
    {
        return GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
    }
}

/// <summary>
/// Represents a resource monitoring session for a single request.
/// This tracks all the metrics and state needed to manage resources for one request's lifecycle.
/// </summary>
internal class ResourceMonitoringSession
{
    public string SessionId { get; set; } = string.Empty;
    public Type RequestType { get; set; } = typeof(object);
    public DateTime StartTime { get; set; }
    public long StartMemoryUsage { get; set; }
    public long CurrentMemoryUsage { get; set; }
    public double MemoryGrowthRate { get; set; }
    public string LastCheckpoint { get; set; } = string.Empty;
    public ConcurrentBag<WeakReference>? TrackedDisposables { get; set; }
}