namespace Doctorly.Infrastructure.Persistence;

/// <summary>
/// Append-only audit snapshot written on every commit that changes an Event.
/// Answers the brief's "preservation of data" question - a queryable history
/// of every version an event has been in, without full event sourcing.
/// </summary>
public sealed class EventRevision
{
    public Guid Id { get; init; }
    public Guid EventId { get; init; }
    public int Version { get; init; }
    public string Snapshot { get; init; } = null!;
    public DateTimeOffset CreatedAtUtc { get; init; }
}
