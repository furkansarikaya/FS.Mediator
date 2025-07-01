namespace FS.Mediator.Models.Streaming;

/// <summary>
/// Context object that flows through the streaming pipeline, carrying metadata and state.
/// This is similar to how HTTP contexts flow through web request pipelines.
/// 
/// The context allows behaviors to:
/// - Share state and metadata across the pipeline
/// - Track performance metrics
/// - Coordinate between different behaviors
/// - Maintain correlation IDs for distributed tracing
/// </summary>
public class StreamingPipelineContext
{
    /// <summary>
    /// Initializes a new streaming pipeline context.
    /// </summary>
    /// <param name="requestType">The type of the streaming request</param>
    /// <param name="correlationId">Optional correlation ID for tracing</param>
    public StreamingPipelineContext(Type requestType, string? correlationId = null)
    {
        RequestType = requestType;
        CorrelationId = correlationId ?? Guid.NewGuid().ToString("N")[..8];
        Properties = new Dictionary<string, object>();
        Metrics = new StreamingOperationMetrics();
    }
    
    /// <summary>
    /// Gets the type of the streaming request being processed.
    /// </summary>
    public Type RequestType { get; }
    
    /// <summary>
    /// Gets the correlation ID for this streaming operation.
    /// Useful for distributed tracing and debugging across multiple services.
    /// </summary>
    public string CorrelationId { get; }
    
    /// <summary>
    /// Gets a dictionary for storing custom properties that behaviors can use to communicate.
    /// For example, a caching behavior might store cache metadata here.
    /// </summary>
    public Dictionary<string, object> Properties { get; }
    
    /// <summary>
    /// Gets the metrics being collected for this streaming operation.
    /// Behaviors can update these metrics to provide insights into performance and health.
    /// </summary>
    public StreamingOperationMetrics Metrics { get; }
    
    /// <summary>
    /// Gets or sets whether the streaming operation has been cancelled.
    /// Behaviors can check this to determine if they should continue processing.
    /// </summary>
    public bool IsCancelled { get; set; }
    
    /// <summary>
    /// Adds or updates a property in the context.
    /// </summary>
    /// <param name="key">The property key</param>
    /// <param name="value">The property value</param>
    public void SetProperty(string key, object value)
    {
        Properties[key] = value;
    }
    
    /// <summary>
    /// Gets a property from the context.
    /// </summary>
    /// <typeparam name="T">The expected type of the property</typeparam>
    /// <param name="key">The property key</param>
    /// <returns>The property value, or default(T) if not found</returns>
    public T? GetProperty<T>(string key)
    {
        return Properties.TryGetValue(key, out var value) && value is T typedValue 
            ? typedValue 
            : default;
    }
}