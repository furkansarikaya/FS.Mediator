namespace FS.Mediator.Features.RequestHandling.Interceptors;

/// <summary>
/// Defines a global request interceptor that can inspect and potentially modify any request
/// regardless of its type. This is useful for implementing cross-cutting concerns that apply
/// to all requests in your system, such as:
/// 
/// - Global security checks
/// - Universal audit logging
/// - System-wide correlation ID injection
/// - Global request validation rules
/// </summary>
public interface IGlobalRequestInterceptor
{
    /// <summary>
    /// Intercepts any request before it enters the pipeline.
    /// This method receives requests as objects, so you'll need to use pattern matching
    /// or reflection if you need to perform type-specific operations.
    /// </summary>
    /// <param name="request">The request to intercept (any type implementing IBaseRequest)</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The request to pass to the pipeline (may be the original or a transformed version)</returns>
    Task<object> InterceptRequestAsync(object request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Determines whether this interceptor should handle the given request type.
    /// This allows global interceptors to selectively process only certain types of requests.
    /// </summary>
    /// <param name="requestType">The type of the request being processed</param>
    /// <returns>True if this interceptor should process the request, false to skip it</returns>
    bool ShouldIntercept(Type requestType) => true;
}