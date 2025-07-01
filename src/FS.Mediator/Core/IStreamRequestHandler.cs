namespace FS.Mediator.Core;

/// <summary>
/// Defines a handler for streaming requests that return an async enumerable of TResponse.
/// This interface is designed for scenarios where you need to process and return large amounts of data
/// without loading everything into memory at once. Perfect for:
/// - Database queries with millions of records
/// - File processing that yields results incrementally  
/// - Real-time data feeds that continue indefinitely
/// - API responses that benefit from progressive loading
/// </summary>
/// <typeparam name="TRequest">The type of streaming request being handled.</typeparam>
/// <typeparam name="TResponse">The type of each item yielded in the stream.</typeparam>
public interface IStreamRequestHandler<in TRequest, out TResponse>
where TRequest : IStreamRequest<TResponse>
{
    /// <summary>
    /// Handles the specified streaming request and yields results asynchronously.
    /// This method should use 'yield return' to provide items one at a time,
    /// allowing the caller to process each item as it becomes available rather than
    /// waiting for the entire result set to be computed.
    /// </summary>
    /// <param name="request">The streaming request to handle.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the streaming operation.</param>
    /// <returns>An async enumerable that yields results as they become available.</returns>
    IAsyncEnumerable<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}