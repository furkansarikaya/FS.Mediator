namespace FS.Mediator.Core;

/// <summary>
/// Represents a unit type for operations that don't return a value.
/// This is used as the response type for requests that only perform actions without returning data.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    /// <summary>
    /// Gets the singleton instance of the Unit type.
    /// </summary>
    public static readonly Unit Value = new();
    
    /// <summary>
    /// Determines whether the specified Unit is equal to the current Unit.
    /// </summary>
    /// <param name="other">The Unit to compare with the current Unit.</param>
    /// <returns>Always returns true since all Unit instances are considered equal.</returns>
    public bool Equals(Unit other) => true;
    
    /// <summary>
    /// Determines whether the specified object is equal to the current Unit.
    /// </summary>
    /// <param name="obj">The object to compare with the current Unit.</param>
    /// <returns>True if the specified object is a Unit; otherwise, false.</returns>
    public override bool Equals(object? obj) => obj is Unit;
    
    /// <summary>
    /// Returns the hash code for this Unit.
    /// </summary>
    /// <returns>Always returns 0 since all Unit instances are considered equal.</returns>
    public override int GetHashCode() => 0;
    
    /// <summary>
    /// Determines whether two Unit instances are equal.
    /// </summary>
    /// <param name="left">The first Unit to compare.</param>
    /// <param name="right">The second Unit to compare.</param>
    /// <returns>Always returns true since all Unit instances are considered equal.</returns>
    public static bool operator ==(Unit left, Unit right) => true;
    
    /// <summary>
    /// Determines whether two Unit instances are not equal.
    /// </summary>
    /// <param name="left">The first Unit to compare.</param>
    /// <param name="right">The second Unit to compare.</param>
    /// <returns>Always returns false since all Unit instances are considered equal.</returns>
    public static bool operator !=(Unit left, Unit right) => false;
    
    /// <summary>
    /// Returns a string representation of the Unit.
    /// </summary>
    /// <returns>The string "()" representing an empty tuple.</returns>
    public override string ToString() => "()";
}