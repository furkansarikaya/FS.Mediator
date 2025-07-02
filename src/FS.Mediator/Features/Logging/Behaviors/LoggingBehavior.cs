using System.Diagnostics;
using FS.Mediator.Features.RequestHandling.Core;
using Microsoft.Extensions.Logging;

namespace FS.Mediator.Features.Logging.Behaviors;

/// <summary>
/// Pipeline behavior that provides logging for request processing.
/// Logs the request type, execution time, and any exceptions that occur.
/// </summary>
/// <typeparam name="TRequest">The type of request being processed.</typeparam>
/// <typeparam name="TResponse">The type of response returned by the request.</typeparam>
public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger) 
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    /// <inheritdoc />
    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Handling request {RequestName}", requestName);

        try
        {
            var response = await next(cancellationToken);
            stopwatch.Stop();
            
            logger.LogInformation("Request {RequestName} handled successfully in {ElapsedMilliseconds}ms", 
                requestName, stopwatch.ElapsedMilliseconds);
            
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            logger.LogError(ex, "Request {RequestName} failed after {ElapsedMilliseconds}ms", 
                requestName, stopwatch.ElapsedMilliseconds);
            
            throw;
        }
    }
}