namespace FS.Mediator.Features.StreamHandling.Core;

/// <summary>
/// Represents a streaming request handler delegate for pipeline behaviors.
/// This is the streaming equivalent of RequestHandlerDelegate but for async enumerables.
/// 
/// The key difference from regular handlers is that this returns an IAsyncEnumerable,
/// which means data flows continuously rather than as a single response.
/// Think of this as a "data faucet" that can be turned on to produce a stream of results.
/// </summary>
/// <typeparam name="TResponse">The type of each item in the stream</typeparam>
/// <returns>An async enumerable that yields results over time</returns>
public delegate IAsyncEnumerable<TResponse> StreamRequestHandlerDelegate<TResponse>(CancellationToken cancellationToken = default);

/// <summary>
/// Defines a pipeline behavior specifically designed for streaming requests.
/// 
/// Streaming behaviors are fundamentally different from regular behaviors because:
/// 1. They work with continuous data flows rather than single responses
/// 2. They need to handle partial success/failure scenarios
/// 3. They must respect the lazy evaluation nature of IAsyncEnumerable
/// 4. They can apply transformations to individual items in the stream
/// 
/// This interface allows you to implement cross-cutting concerns like logging,
/// error handling, and data transformation for streaming operations while
/// maintaining the performance benefits of streaming (low memory usage, 
/// immediate availability of first results, etc.).
/// </summary>
/// <typeparam name="TRequest">The type of streaming request being processed</typeparam>
/// <typeparam name="TResponse">The type of each item yielded by the stream</typeparam>
public interface IStreamPipelineBehavior<in TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    /// <summary>
    /// Handles the streaming request within the pipeline.
    /// 
    /// This method wraps around the next handler in the pipeline and can:
    /// - Log the start and end of streaming operations
    /// - Transform individual items as they flow through
    /// - Handle errors that occur during streaming
    /// - Implement retry logic for failed streams
    /// - Apply circuit breaker patterns to protect downstream services
    /// - Cache or buffer stream results
    /// 
    /// Important: Unlike regular behaviors, this method must preserve the streaming
    /// nature of the operation. Don't convert the IAsyncEnumerable to a list or
    /// array unless absolutely necessary, as this defeats the purpose of streaming.
    /// </summary>
    /// <param name="request">The streaming request being processed</param>
    /// <param name="next">The next handler in the pipeline (could be another behavior or the actual handler)</param>
    /// <param name="cancellationToken">Cancellation token for the streaming operation</param>
    /// <returns>An async enumerable that yields transformed/processed results</returns>
    IAsyncEnumerable<TResponse> HandleAsync(
        TRequest request, 
        StreamRequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default);
}
