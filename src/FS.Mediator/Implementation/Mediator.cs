using System.Runtime.CompilerServices;
using FS.Mediator.Core;
using FS.Mediator.Exceptions;

namespace FS.Mediator.Implementation;

/// <summary>
/// Default implementation of the IMediator interface.
/// Provides request/response and notification publishing capabilities with pipeline behavior support.
/// </summary>
public class Mediator(ServiceFactory serviceFactory, ServiceFactoryCollection serviceFactoryCollection) : IMediator
{
    /// <inheritdoc />
    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var responseType = typeof(TResponse);
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);

        var handler = serviceFactory(handlerType);
        if (handler == null)
            throw new HandlerNotFoundException(handlerType);

        // Get pipeline behaviors - Fix: Use correct generic type parameters
        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);
        var behaviorObjects = serviceFactoryCollection(behaviorType).ToList();

        // Build pipeline starting with the actual handler
        RequestHandlerDelegate<TResponse> handlerDelegate = (ct) =>
        {
            var handleMethod = handlerType.GetMethod(nameof(IRequestHandler<IRequest<TResponse>, TResponse>.HandleAsync));
            var result = handleMethod?.Invoke(handler, [request, ct]);
            return (Task<TResponse>)result!;
        };

        // Execute behaviors in reverse order using reflection
        // This approach avoids the generic casting issue by working with reflection throughout
        foreach (var behaviorObj in behaviorObjects.AsEnumerable().Reverse())
        {
            var currentHandler = handlerDelegate;
            handlerDelegate = (ct) =>
            {
                // Use reflection to call the HandleAsync method on the behavior
                var behaviorHandleMethod = behaviorObj.GetType().GetMethod("HandleAsync");
                if (behaviorHandleMethod == null) return currentHandler(ct);
                // Create a delegate that represents the next handler in the pipeline
                var nextDelegate = new RequestHandlerDelegate<TResponse>(currentHandler);
                    
                // Invoke the behavior's HandleAsync method
                var result = behaviorHandleMethod.Invoke(behaviorObj, new object[] { request, nextDelegate, ct });
                return (Task<TResponse>)result!;
            };
        }

        return await handlerDelegate(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendAsync(IRequest<Unit> request, CancellationToken cancellationToken = default)
    {
        await SendAsync<Unit>(request, cancellationToken);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        var requestType = request.GetType();
        var responseType = typeof(TResponse);
        var handlerType = typeof(IStreamRequestHandler<,>).MakeGenericType(requestType, responseType);

        var handler = serviceFactory(handlerType);
        if (handler == null)
            throw new HandlerNotFoundException(handlerType);

        // For streaming, we get the handler method and invoke it
        // Note: Pipeline behaviors for streaming could be added in future versions
        // but require more complex implementation due to IAsyncEnumerable nature
        var handleMethod = handlerType.GetMethod(nameof(IStreamRequestHandler<IStreamRequest<TResponse>, TResponse>.HandleAsync));
        var result = handleMethod?.Invoke(handler, [request, cancellationToken]);
        
        if (result is IAsyncEnumerable<TResponse> asyncEnumerable)
        {
            // Here's where the magic happens: we yield each item as it becomes available
            // This allows the caller to start processing results immediately, even if
            // the handler is still producing more results in the background
            await foreach (var item in asyncEnumerable.WithCancellation(cancellationToken))
            {
                yield return item;
            }
        }
        else
        {
            throw new InvalidOperationException($"Handler method did not return IAsyncEnumerable<{responseType.Name}>");
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<object?> CreateStream(object request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        var requestType = request.GetType();
        var responseType = typeof(object);
        var handlerType = typeof(IStreamRequestHandler<,>).MakeGenericType(requestType, responseType);

        var handler = serviceFactory(handlerType);
        if (handler == null)
            throw new HandlerNotFoundException(handlerType);

        // For streaming, we get the handler method and invoke it
        // Note: Pipeline behaviors for streaming could be added in future versions
        // but require more complex implementation due to IAsyncEnumerable nature
        var handleMethod = handlerType.GetMethod(nameof(IStreamRequestHandler<IStreamRequest<object>, object>.HandleAsync));
        var result = handleMethod?.Invoke(handler, [request, cancellationToken]);
        
        if (result is IAsyncEnumerable<object> asyncEnumerable)
        {
            // Here's where the magic happens: we yield each item as it becomes available
            // This allows the caller to start processing results immediately, even if
            // the handler is still producing more results in the background
            await foreach (var item in asyncEnumerable.WithCancellation(cancellationToken))
            {
                yield return item;
            }
        }
        else
        {
            throw new InvalidOperationException($"Handler method did not return IAsyncEnumerable<{responseType.Name}>");
        }
    }

    /// <inheritdoc />
    public async Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);
        
        var notificationType = typeof(TNotification);
        var handlerType = typeof(INotificationHandler<>).MakeGenericType(notificationType);

        var handlers = serviceFactoryCollection(handlerType)
            .Cast<INotificationHandler<TNotification>>()
            .ToList();

        var tasks = handlers.Select(handler => handler.HandleAsync(notification, cancellationToken));
        
        await Task.WhenAll(tasks);
    }
}