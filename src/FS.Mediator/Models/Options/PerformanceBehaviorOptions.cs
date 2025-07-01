namespace FS.Mediator.Models.Options;

/// <summary>
/// Configuration options for the PerformanceBehavior.
/// </summary>
public class PerformanceBehaviorOptions
{
    /// <summary>
    /// Gets or sets the warning threshold in milliseconds for logging performance warnings.
    /// Default is 500ms.
    /// </summary>
    public int WarningThresholdMs { get; set; } = 500;
}