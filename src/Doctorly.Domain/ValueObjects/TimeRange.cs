using Doctorly.Domain.Exceptions;

namespace Doctorly.Domain.ValueObjects;

public sealed record TimeRange
{
    public DateTimeOffset Start { get; }
    public DateTimeOffset End { get; }

    public TimeRange(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
            throw new DomainException("End time must be after start time.");

        // Postgres timestamptz (via Npgsql) only accepts DateTimeOffset with a zero
        // offset - normalize here so every non-UTC caller doesn't have to know that
        Start = start.ToUniversalTime();
        End = end.ToUniversalTime();
    }

    public bool Overlaps(TimeRange other) => Start < other.End && other.Start < End;
}
