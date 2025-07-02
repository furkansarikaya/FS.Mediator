namespace FS.Mediator.Features.NotificationHandling.Core;

/// <summary>
/// Defines a handler for a notification of type TNotification.
/// Multiple handlers can exist for the same notification type.
/// </summary>
/// <typeparam name="TNotification">The type of notification being handled.</typeparam>
public interface INotificationHandler<in TNotification> 
    where TNotification : INotification
{
    /// <summary>
    /// Handles the specified notification asynchronously.
    /// </summary>
    /// <param name="notification">The notification to handle.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task HandleAsync(TNotification notification, CancellationToken cancellationToken = default);
}