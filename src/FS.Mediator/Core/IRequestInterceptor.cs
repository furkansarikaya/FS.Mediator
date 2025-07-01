namespace FS.Mediator.Core;

/// <summary>
/// Defines a request interceptor that can inspect and potentially modify requests
/// before they enter the mediator pipeline. Request interceptors are executed
/// before pipeline behaviors and are perfect for:
/// 
/// - Request validation and sanitization
/// - Security checks and authorization
/// - Request transformation and mapping
/// - Audit logging of incoming requests
/// - Adding correlation IDs or tracing information
/// 
/// Unlike pipeline behaviors, interceptors focus specifically on request/response
/// transformation rather than cross-cutting concerns like retry logic or circuit breaking.
/// </summary>
/// <typeparam name="TRequest">The type of request being intercepted</typeparam>
/// <typeparam name="TResponse">The type of response expected from the request</typeparam>
public interface IRequestInterceptor<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Intercepts and potentially transforms a request before it enters the pipeline.
    /// This method is called for every request of the specified type and allows you to:
    /// 
    /// - Validate the request and throw exceptions if invalid
    /// - Transform or enrich the request with additional data
    /// - Log or audit the incoming request
    /// - Perform security checks or authorization
    /// 
    /// The interceptor can either return the original request unchanged,
    /// return a modified version of the request, or throw an exception to prevent processing.
    /// </summary>
    /// <param name="request">The original request to intercept</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The request to pass to the pipeline (may be the original or a transformed version)</returns>
    /// <exception cref="ArgumentException">Thrown when the request is invalid</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when authorization fails</exception>
    Task<TRequest> InterceptRequestAsync(TRequest request, CancellationToken cancellationToken = default);
}