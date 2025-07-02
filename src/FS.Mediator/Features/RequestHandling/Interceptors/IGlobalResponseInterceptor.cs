namespace FS.Mediator.Features.RequestHandling.Interceptors;

/// <summary>
/// Defines a global response interceptor that can inspect and potentially modify any response
/// regardless of its type. This is useful for implementing cross-cutting concerns that apply
/// to all responses in your system, such as:
/// 
/// - Global response logging and auditing
/// - Universal performance metrics collection
/// - System-wide response transformation
/// - Global caching strategies
/// - Security-related response filtering
/// </summary>
public interface IGlobalResponseInterceptor
{
    /// <summary>
    /// Intercepts any response after pipeline processing.
    /// This method receives both requests and responses as objects, so you'll need to use 
    /// pattern matching or reflection if you need to perform type-specific operations.
    /// </summary>
    /// <param name="request">The original request that generated this response</param>
    /// <param name="response">The response from the pipeline to intercept</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The response to return to the caller (may be the original or a transformed version)</returns>
    Task<object?> InterceptResponseAsync(object request, object? response, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Determines whether this interceptor should handle the given request/response types.
    /// This allows global interceptors to selectively process only certain types of operations.
    /// </summary>
    /// <param name="requestType">The type of the request being processed</param>
    /// <param name="responseType">The type of the response being processed</param>
    /// <returns>True if this interceptor should process the response, false to skip it</returns>
    bool ShouldIntercept(Type requestType, Type responseType) => true;
}