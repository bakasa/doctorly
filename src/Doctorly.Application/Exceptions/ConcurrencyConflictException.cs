namespace Doctorly.Application.Exceptions;

/// <summary>
/// Thrown when a caller's expected version doesn't match the current version of an event.
/// Maps to 412 Precondition Failed at the API boundary.
/// </summary>
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message) : base(message)
    {
    }
}
