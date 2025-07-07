using System.Runtime.CompilerServices;
using FS.Mediator.Features.NotificationHandling.Core;
using FS.Mediator.Features.RequestHandling.Core;
using FS.Mediator.Features.RequestHandling.Exceptions;
using FS.Mediator.Features.RequestHandling.Interceptors;
using FS.Mediator.Features.StreamHandling.Core;

namespace FS.Mediator.Features.RequestHandling.Implementation;

/// <summary>
/// Enhanced implementation of the IMediator interface with interceptor support.
/// This implementation provides a sophisticated request processing pipeline that includes:
/// 
/// 1. Request Interception: Transforms or validates requests before processing
/// 2. Pipeline Behaviors: Cross-cutting concerns like logging, retry, circuit breaking
/// 3. Handler Execution: The actual business logic processing
/// 4. Response Interception: Transforms or enriches responses before returning
/// 
/// This layered approach provides maximum flexibility while maintaining clean separation of concerns.
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

        // Step 1: Execute Request Interceptors
        // These run first and can transform or validate the incoming request
        var interceptedRequest = await ExecuteRequestInterceptorsAsync<IRequest<TResponse>, TResponse>(request, cancellationToken);

        // Step 2: Build and Execute Pipeline with Behaviors
        // This is where cross-cutting concerns like retry, circuit breaking, and logging happen
        var response = await ExecutePipelineAsync<TResponse>(interceptedRequest, handler, handlerType, cancellationToken);

        // Step 3: Execute Response Interceptors
        // These run last and can transform or enrich the outgoing response
        var interceptedResponse = await ExecuteResponseInterceptorsAsync(interceptedRequest, response, cancellationToken);

        return interceptedResponse;
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

        // Execute the streaming pipeline with behaviors
        // This is where the magic happens - streaming behaviors can now wrap around stream operations
        await foreach (var item in ExecuteStreamingPipelineAsync<TResponse>(request, handler, handlerType, cancellationToken))
        {
            yield return item;
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

        var handleMethod = handlerType.GetMethod(nameof(IStreamRequestHandler<IStreamRequest<object>, object>.HandleAsync));
        var result = handleMethod?.Invoke(handler, [request, cancellationToken]);
        
        if (result is IAsyncEnumerable<object> asyncEnumerable)
        {
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
    public async Task PublishAsync(object notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        
        // Verify that the notification implements INotification
        if (notification is not INotification notificationInstance)
        {
            throw new ArgumentException($"Notification of type '{notification.GetType().Name}' must implement INotification interface.", nameof(notification));
        }
        
        var notificationType = notification.GetType();
        var handlerType = typeof(INotificationHandler<>).MakeGenericType(notificationType);

        var handlers = serviceFactoryCollection(handlerType).ToList();
        
        if (handlers.Count == 0)
        {
            // Optionally, you can choose to throw an exception or just log a warning
            // For now, we'll silently return (consistent with typical mediator behavior)
            return;
        }

        var tasks = new List<Task>();
        
        foreach (var handler in handlers)
        {
            // Use reflection to call HandleAsync method on each handler
            var handleMethod = handlerType.GetMethod(nameof(INotificationHandler<INotification>.HandleAsync));
            if (handleMethod == null) continue;
            var result = handleMethod.Invoke(handler, [notification, cancellationToken]);
            if (result is Task task)
            {
                tasks.Add(task);
            }
        }
        
        await Task.WhenAll(tasks);
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

    /// <summary>
    /// Executes all registered request interceptors for the given request.
    /// This method provides a powerful extension point for request transformation and validation.
    /// </summary>
    private async Task<TRequest> ExecuteRequestInterceptorsAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
    {
        var requestType = typeof(TRequest);
        var responseType = typeof(TResponse);
        
        // Get typed interceptors first - these are the most specific and run first
        var typedInterceptorType = typeof(IRequestInterceptor<,>).MakeGenericType(requestType, responseType);
        var typedInterceptors = serviceFactoryCollection(typedInterceptorType);
        
        var currentRequest = request;
        
        // Execute typed interceptors in registration order
        foreach (var interceptor in typedInterceptors)
        {
            var interceptMethod = typedInterceptorType.GetMethod(nameof(IRequestInterceptor<TRequest, TResponse>.InterceptRequestAsync));
            var result = interceptMethod?.Invoke(interceptor, [currentRequest, cancellationToken]);
            if (result is Task<TRequest> task)
            {
                currentRequest = await task;
            }
        }
        
        // Get global interceptors and execute them after typed interceptors
        var globalInterceptors = serviceFactoryCollection(typeof(IGlobalRequestInterceptor))
            .Cast<IGlobalRequestInterceptor>()
            .Where(gi => gi.ShouldIntercept(requestType));
        
        object currentRequestObj = currentRequest;
        foreach (var globalInterceptor in globalInterceptors)
        {
            currentRequestObj = await globalInterceptor.InterceptRequestAsync(currentRequestObj, cancellationToken);
        }
        
        // The global interceptors work with objects, so we need to cast back
        // In a real implementation, you might want to add type safety checks here
        return (TRequest)currentRequestObj;
    }

    /// <summary>
    /// Executes the main processing pipeline including all registered behaviors.
    /// This is where the core mediator pattern happens, with cross-cutting concerns applied.
    /// </summary>
    private async Task<TResponse> ExecutePipelineAsync<TResponse>(
        IRequest<TResponse> request, 
        object handler, 
        Type handlerType, 
        CancellationToken cancellationToken)
    {
        var requestType = request.GetType();
        var responseType = typeof(TResponse);
        
        // Get pipeline behaviors - these provide cross-cutting concerns
        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);
        var behaviorObjects = serviceFactoryCollection(behaviorType).ToList();

        // Build the pipeline starting with the actual handler at the core
        RequestHandlerDelegate<TResponse> handlerDelegate = (ct) =>
        {
            var handleMethod = handlerType.GetMethod(nameof(IRequestHandler<IRequest<TResponse>, TResponse>.HandleAsync));
            var result = handleMethod?.Invoke(handler, [request, ct]);
            return (Task<TResponse>)result!;
        };

        // Wrap the handler with behaviors in reverse order (last registered behavior wraps first)
        // This creates a nested structure where each behavior can call the next one in the chain
        foreach (var behaviorObj in behaviorObjects.AsEnumerable().Reverse())
        {
            var currentHandler = handlerDelegate;
            handlerDelegate = (ct) =>
            {
                var behaviorHandleMethod = behaviorObj.GetType().GetMethod("HandleAsync");
                if (behaviorHandleMethod == null) return currentHandler(ct);
                
                var nextDelegate = new RequestHandlerDelegate<TResponse>(currentHandler);
                var result = behaviorHandleMethod.Invoke(behaviorObj, new object[] { request, nextDelegate, ct });
                return (Task<TResponse>)result!;
            };
        }

        return await handlerDelegate(cancellationToken);
    }

    /// <summary>
    /// Executes all registered response interceptors for the given response.
    /// This method provides the final opportunity to transform or enrich responses.
    /// </summary>
    private async Task<TResponse> ExecuteResponseInterceptorsAsync<TRequest, TResponse>(
        TRequest request, 
        TResponse response, 
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
    {
        var requestType = typeof(TRequest);
        var responseType = typeof(TResponse);
        
        // Get typed interceptors first - these are the most specific
        var typedInterceptorType = typeof(IResponseInterceptor<,>).MakeGenericType(requestType, responseType);
        var typedInterceptors = serviceFactoryCollection(typedInterceptorType);
        
        var currentResponse = response;
        
        // Execute typed interceptors in registration order
        foreach (var interceptor in typedInterceptors)
        {
            var interceptMethod = typedInterceptorType.GetMethod(nameof(IResponseInterceptor<TRequest, TResponse>.InterceptResponseAsync));
            var result = interceptMethod?.Invoke(interceptor, [request, currentResponse, cancellationToken]);
            if (result is Task<TResponse> task)
            {
                currentResponse = await task;
            }
        }
        
        // Get global interceptors and execute them after typed interceptors
        var globalInterceptors = serviceFactoryCollection(typeof(IGlobalResponseInterceptor))
            .Cast<IGlobalResponseInterceptor>()
            .Where(gi => gi.ShouldIntercept(requestType, responseType));
        
        object? currentResponseObj = currentResponse;
        foreach (var globalInterceptor in globalInterceptors)
        {
            currentResponseObj = await globalInterceptor.InterceptResponseAsync(request, currentResponseObj, cancellationToken);
        }
        
        // Cast back from object - in production code, you'd want more robust type checking
        return (TResponse)currentResponseObj!;
    }

    /// <summary>
    /// Executes the streaming processing pipeline including all registered streaming behaviors.
    /// This is the heart of streaming operations - where cross-cutting concerns like logging,
    /// retry logic, and circuit breaking are applied to continuous data flows.
    /// 
    /// The key insight here is that streaming behaviors work fundamentally differently
    /// from regular behaviors because they operate on IAsyncEnumerable rather than single values.
    /// Each behavior in the chain can transform, filter, or enrich the stream of data
    /// as it flows through the pipeline.
    /// </summary>
    private async IAsyncEnumerable<TResponse> ExecuteStreamingPipelineAsync<TResponse>(
        IStreamRequest<TResponse> request, 
        object handler, 
        Type handlerType, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var requestType = request.GetType();
        var responseType = typeof(TResponse);
        
        // Get streaming pipeline behaviors - these are specialized for stream operations
        var behaviorType = typeof(IStreamPipelineBehavior<,>).MakeGenericType(requestType, responseType);
        var behaviorObjects = serviceFactoryCollection(behaviorType).ToList();

        // Build the streaming pipeline starting with the actual handler at the core
        // This creates a nested structure where each behavior wraps the next one in the chain
        StreamRequestHandlerDelegate<TResponse> handlerDelegate = (ct) =>
        {
            var handleMethod = handlerType.GetMethod(nameof(IStreamRequestHandler<IStreamRequest<TResponse>, TResponse>.HandleAsync));
            var result = handleMethod?.Invoke(handler, [request, ct]);
            
            if (result is IAsyncEnumerable<TResponse> asyncEnumerable)
            {
                return asyncEnumerable;
            }
            
            throw new InvalidOperationException($"Handler method did not return IAsyncEnumerable<{responseType.Name}>");
        };

        // Wrap the handler with streaming behaviors in reverse order
        // This creates a chain where the outer behavior executes first and can control the inner behaviors
        // For streaming, this means outer behaviors see the complete stream and can apply transformations
        foreach (var behaviorObj in behaviorObjects.AsEnumerable().Reverse())
        {
            var currentHandler = handlerDelegate;
            handlerDelegate = (ct) =>
            {
                // Use reflection to call the HandleAsync method on the streaming behavior
                var behaviorHandleMethod = behaviorObj.GetType().GetMethod("HandleAsync");
                if (behaviorHandleMethod == null) return currentHandler(ct);
                
                // Create a delegate that represents the next handler in the streaming pipeline
                var nextDelegate = new StreamRequestHandlerDelegate<TResponse>(currentHandler);
                    
                // Invoke the behavior's HandleAsync method and return the resulting stream
                var result = behaviorHandleMethod.Invoke(behaviorObj, new object[] { request, nextDelegate, ct });
                return (IAsyncEnumerable<TResponse>)result!;
            };
        }

        // Execute the complete pipeline and yield each item as it becomes available
        // This is where the streaming magic happens - data flows through all behaviors
        // and reaches the caller immediately, without buffering the entire result set
        await foreach (var item in handlerDelegate(cancellationToken))
        {
            yield return item;
        }
    }
}