using FS.Mediator.Features.HealthChecking.Models;

namespace FS.Mediator.Features.HealthChecking.Services;

/// <summary>
/// Interface for health reporting services.
/// This abstraction allows different implementations for various monitoring systems
/// (Application Insights, Prometheus, custom dashboards, etc.).
/// </summary>
public interface IStreamHealthReporter
{
    /// <summary>
    /// Reports current health metrics to the monitoring system.
    /// This method should be lightweight and non-blocking to avoid impacting stream performance.
    /// </summary>
    Task ReportHealthAsync(StreamHealthMetrics metrics, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Reports a critical health issue that requires immediate attention.
    /// This might trigger alerts, notifications, or automatic remediation actions.
    /// </summary>
    Task ReportCriticalIssueAsync(StreamHealthMetrics metrics, HealthWarning warning, CancellationToken cancellationToken = default);
}