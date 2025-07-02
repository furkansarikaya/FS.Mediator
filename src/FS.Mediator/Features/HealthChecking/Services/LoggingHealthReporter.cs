using FS.Mediator.Features.HealthChecking.Models;
using FS.Mediator.Features.HealthChecking.Models.Enums;
using Microsoft.Extensions.Logging;

namespace FS.Mediator.Features.HealthChecking.Services;

/// <summary>
/// Default implementation of health reporter that logs health information.
/// This provides basic health reporting functionality that works out-of-the-box
/// with standard .NET logging infrastructure.
/// </summary>
public class LoggingHealthReporter(ILogger<LoggingHealthReporter> logger) : IStreamHealthReporter
{
    public Task ReportHealthAsync(StreamHealthMetrics metrics, CancellationToken cancellationToken = default)
    {
        // Create a comprehensive health report
        var healthData = new
        {
            CorrelationId = metrics.CorrelationId,
            RequestType = metrics.RequestTypeName,
            HealthStatus = metrics.HealthStatus.ToString(),
            TotalItems = metrics.TotalItems,
            CurrentThroughput = metrics.CurrentThroughput,
            PeakThroughput = metrics.PeakThroughput,
            MemoryUsageMB = metrics.CurrentMemoryUsage / 1_000_000,
            MemoryGrowthMB = (metrics.CurrentMemoryUsage - metrics.StartMemoryUsage) / 1_000_000,
            ErrorCount = metrics.ErrorCount,
            TimeSinceLastItemSeconds = metrics.TimeSinceLastItem.TotalSeconds,
            WarningCount = metrics.HealthWarnings.Count
        };
        
        // Log based on health status
        switch (metrics.HealthStatus)
        {
            case StreamHealthStatus.Healthy:
                logger.LogDebug("Stream health report: {@HealthData}", healthData);
                break;
                
            case StreamHealthStatus.Warning:
                logger.LogWarning("Stream health warning: {@HealthData}", healthData);
                break;
                
            case StreamHealthStatus.Unhealthy:
                logger.LogError("Stream unhealthy: {@HealthData}", healthData);
                break;
                
            case StreamHealthStatus.Failed:
                logger.LogCritical("Stream failed: {@HealthData}", healthData);
                break;
        }
        
        return Task.CompletedTask;
    }
    
    public Task ReportCriticalIssueAsync(StreamHealthMetrics metrics, HealthWarning warning, CancellationToken cancellationToken = default)
    {
        logger.LogCritical("Critical stream issue detected - Type: {WarningType}, Message: {Message}, Stream: {CorrelationId}, Details: {Details}",
            warning.Type, warning.Message, metrics.CorrelationId, warning.Details);
        
        return Task.CompletedTask;
    }
}