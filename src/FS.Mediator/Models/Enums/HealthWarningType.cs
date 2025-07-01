namespace FS.Mediator.Models.Enums;

/// <summary>
/// Types of health warnings that can be detected during streaming operations.
/// Each type represents a different category of potential issue.
/// </summary>
public enum HealthWarningType
{
    LowThroughput,
    HighMemoryUsage,
    StreamStalled,
    HighErrorRate,
    ErrorOccurred,
    ResourceExhaustion,
    PerformanceDegradation
}