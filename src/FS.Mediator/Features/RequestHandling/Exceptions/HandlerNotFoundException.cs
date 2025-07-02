namespace FS.Mediator.Features.RequestHandling.Exceptions;

/// <summary>
/// Exception thrown when no handler is found for a specific request or notification type.
/// </summary>
public class HandlerNotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance of the HandlerNotFoundException class.
    /// </summary>
    /// <param name="handlerType">The type of handler that was not found.</param>
    public HandlerNotFoundException(Type handlerType) 
        : base($"No handler found for type '{handlerType.Name}'.")
    {
        HandlerType = handlerType;
    }
    
    /// <summary>
    /// Initializes a new instance of the HandlerNotFoundException class with a custom message.
    /// </summary>
    /// <param name="message">The custom error message.</param>
    /// <param name="handlerType">The type of handler that was not found.</param>
    public HandlerNotFoundException(string message, Type handlerType) 
        : base(message)
    {
        HandlerType = handlerType;
    }
    
    /// <summary>
    /// Gets the type of handler that was not found.
    /// </summary>
    public Type HandlerType { get; }
}