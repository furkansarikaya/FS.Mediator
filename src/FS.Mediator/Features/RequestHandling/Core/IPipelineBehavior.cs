namespace FS.Mediator.Features.RequestHandling.Core;

/// <summary>
/// Represents a request handler delegate for pipeline behaviors.
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the handler.</typeparam>
/// <returns>A task representing the asynchronous operation that returns the response.</returns>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken cancellationToken = default);

/// <summary>
/// Defines a pipeline behavior that can be applied to requests before they reach their handlers.
/// Pipeline behaviors are executed in the order they are registered and can perform cross-cutting concerns
/// such as logging, validation, caching, or exception handling.
/// </summary>
/// <typeparam name="TRequest">The type of request being processed.</typeparam>
/// <typeparam name="TResponse">The type of response returned by the request.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Handles the request within the pipeline.
    /// </summary>
    /// <param name="request">The request being processed.</param>
    /// <param name="next">The next handler in the pipeline.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation that returns the response.</returns>
    Task<TResponse> HandleAsync(TRequest request, 
        RequestHandlerDelegate<TResponse> next,
    CancellationToken cancellationToken = default);
}