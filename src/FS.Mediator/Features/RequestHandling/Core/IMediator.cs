using FS.Mediator.Features.NotificationHandling.Core;
using FS.Mediator.Features.RequestHandling.Exceptions;
using FS.Mediator.Features.StreamHandling.Core;

namespace FS.Mediator.Features.RequestHandling.Core;

/// <summary>
/// Defines the mediator interface for sending requests and publishing notifications.
/// This is the main entry point for all request/response and notification operations.
/// </summary>
public interface IMediator
{
    /// <summary>
    /// Sends a request and returns the response asynchronously.
    /// </summary>
    /// <typeparam name="TResponse">The type of response expected from the request.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation that returns the response.</returns>
    /// <exception cref="HandlerNotFoundException">Thrown when no handler is found for the request type.</exception>
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request that doesn't return a specific value (returns Unit) asynchronously.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="HandlerNotFoundException">Thrown when no handler is found for the request type.</exception>
    Task SendAsync(IRequest<Unit> request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a data stream from a streaming request and returns results as they become available.
    /// This is perfect for scenarios where you need to process large amounts of data efficiently:
    /// 
    /// Example use cases:
    /// - Processing millions of database records without loading them all into memory
    /// - Reading large files and yielding processed lines incrementally
    /// - Creating real-time data feeds that continuously produce results
    /// - Building APIs that can start returning data before all processing is complete
    /// 
    /// The key benefit is that your application can start processing and displaying results
    /// immediately as they become available, rather than waiting for everything to complete.
    /// </summary>
    /// <typeparam name="TResponse">The type of each item in the stream.</typeparam>
    /// <param name="request">The streaming request to process.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the streaming operation.</param>
    /// <returns>An async enumerable that yields results as they become available.</returns>
    /// <exception cref="HandlerNotFoundException">Thrown when no handler is found for the request type.</exception>

    IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a data stream from a streaming request and returns results as they become available.
    /// This is perfect for scenarios where you need to process large amounts of data efficiently:
    /// 
    /// Example use cases:
    /// - Processing millions of database records without loading them all into memory
    /// - Reading large files and yielding processed lines incrementally
    /// - Creating real-time data feeds that continuously produce results
    /// - Building APIs that can start returning data before all processing is complete
    /// 
    /// The key benefit is that your application can start processing and displaying results
    /// immediately as they become available, rather than waiting for everything to complete.
    /// </summary>
    /// <param name="request">The streaming request to process.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the streaming operation.</param>
    /// <returns>An async enumerable that yields results as they become available.</returns>
    /// <exception cref="HandlerNotFoundException">Thrown when no handler is found for the request type.</exception>
    IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Publishes a notification to all registered handlers asynchronously.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification to publish.</typeparam>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification;
}