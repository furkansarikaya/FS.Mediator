namespace FS.Mediator.Features.RequestHandling.Implementation;

/// <summary>
/// Factory delegate for creating service instances.
/// </summary>
/// <param name="serviceType">The type of service to create.</param>
/// <returns>An instance of the requested service type.</returns>
public delegate object ServiceFactory(Type serviceType);

/// <summary>
/// Factory delegate for creating multiple service instances.
/// </summary>
/// <param name="serviceType">The type of services to create.</param>
/// <returns>A collection of instances of the requested service type.</returns>
public delegate IEnumerable<object> ServiceFactoryCollection(Type serviceType);