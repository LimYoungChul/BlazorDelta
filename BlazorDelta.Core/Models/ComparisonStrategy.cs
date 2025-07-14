namespace BlazorDelta.Core.Models;

public enum ComparisonStrategy
{
    Value,      // Use == or != 
    Reference,  // Use ReferenceEquals
    Never,      // Always assign without comparison
    Generic,     // Use EqualityComparer<T>.Default for generic types
    EventCallback  // Use .Equals() for EventCallback structs
}
