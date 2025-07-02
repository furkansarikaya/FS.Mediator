namespace FS.Mediator.Features.RequestHandling.Core;

/// <summary>
/// Allows for generic type constraints of objects implementing IRequest or IRequest{TResponse}
/// </summary>
public interface IBaseRequest { }

/// <summary>
/// Marker interface for requests that return a response of type Unit.
/// Implement this interface to create request objects that can be handled by the mediator.
/// </summary>
public interface IRequest : IBaseRequest { }

/// <summary>
/// Marker interface for requests that return a response of type TResponse.
/// Implement this interface to create request objects that can be handled by the mediator.
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the request.</typeparam>
public interface IRequest<out TResponse> : IBaseRequest { }