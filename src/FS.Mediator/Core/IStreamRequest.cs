namespace FS.Mediator.Core;

/// <summary>
/// Marker interface for streaming requests that return an async enumerable of TResponse.
/// Use this interface when you need to stream large amounts of data or provide real-time updates.
/// This is particularly useful for scenarios like:
/// - Large data set queries (avoiding memory issues)
/// - Real-time data feeds
/// - Progressive data loading
/// </summary>
/// <typeparam name="TResponse">The type of each item in the stream.</typeparam>
public interface IStreamRequest<out TResponse> { }