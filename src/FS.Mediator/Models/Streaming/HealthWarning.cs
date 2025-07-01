using FS.Mediator.Models.Enums;

namespace FS.Mediator.Models.Streaming;

/// <summary>
/// Represents a specific health warning detected during stream processing.
/// This provides detailed information about what issue was detected and when.
/// </summary>
public class HealthWarning
{
    public DateTime Timestamp { get; set; }
    public HealthWarningType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? Recommendation { get; set; }
}
