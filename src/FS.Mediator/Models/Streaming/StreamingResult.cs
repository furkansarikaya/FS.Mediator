namespace FS.Mediator.Models.Streaming;

/// <summary>
/// Represents the result of a streaming operation with error information.
/// This allows us to handle errors without using try-catch around yield statements.
/// </summary>
public class StreamingResult<T>
{
    public T? Value { get; init; }
    public Exception? Error { get; init; }
    public bool IsSuccess => Error == null;
    public bool IsError => Error != null;
    
    public static StreamingResult<T> Success(T value) => new() { Value = value };
    public static StreamingResult<T> Failure(Exception error) => new() { Error = error };
}