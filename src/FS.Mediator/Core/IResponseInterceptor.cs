namespace FS.Mediator.Core;

/// <summary>
/// Defines a response interceptor that can inspect and potentially modify responses
/// after they're returned from the mediator pipeline but before they reach the caller.
/// Response interceptors are perfect for:
/// 
/// - Response transformation and mapping
/// - Adding metadata or enriching responses
/// - Response validation and sanitization
/// - Caching or storing responses
/// - Audit logging of outgoing responses
/// - Performance metrics collection
/// 
/// Response interceptors execute after all pipeline behaviors have completed successfully,
/// giving you the final opportunity to modify or enrich the response data.
/// </summary>
/// <typeparam name="TRequest">The type of request that generated this response</typeparam>
/// <typeparam name="TResponse">The type of response being intercepted</typeparam>
public interface IResponseInterceptor<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Intercepts and potentially transforms a response after pipeline processing.
    /// This method is called for every successful response of the specified type and allows you to:
    /// 
    /// - Transform or enrich the response with additional data
    /// - Cache the response for future requests
    /// - Log or audit the outgoing response
    /// - Add metadata like timestamps or correlation IDs
    /// - Validate the response before returning it
    /// 
    /// The interceptor can either return the original response unchanged,
    /// return a modified version of the response, or throw an exception.
    /// </summary>
    /// <param name="request">The original request that generated this response</param>
    /// <param name="response">The response from the pipeline to intercept</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The response to return to the caller (may be the original or a transformed version)</returns>
    Task<TResponse> InterceptResponseAsync(TRequest request, TResponse response, CancellationToken cancellationToken = default);
}