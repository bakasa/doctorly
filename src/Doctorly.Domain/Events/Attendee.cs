using Doctorly.Domain.Exceptions;
using Doctorly.Domain.ValueObjects;

namespace Doctorly.Domain.Events;

public sealed class Attendee
{
    public const int MaxNameLength = 100;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public EmailAddress Email { get; private set; } = null!;

    // null = no response yet
    public bool? IsAttending { get; private set; }

    private Attendee()
    {
    }

    internal Attendee(string name, EmailAddress email, bool? isAttending = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Attendee name is required.");

        if (name.Length > MaxNameLength)
            throw new DomainException($"Attendee name must be {MaxNameLength} characters or fewer.");

        Id = Guid.NewGuid();
        Name = name;
        Email = email;
        IsAttending = isAttending;
    }

    internal void Respond(bool isAttending) => IsAttending = isAttending;
}
