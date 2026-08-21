using Doctorly.Domain.DomainEvents;
using Doctorly.Domain.Exceptions;
using Doctorly.Domain.ValueObjects;

namespace Doctorly.Domain.Events;

public sealed class Event
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 2000;

    private readonly List<Attendee> _attendees = [];
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; private set; }
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public TimeRange TimeRange { get; private set; } = null!;
    public EventStatus Status { get; private set; }

    // optimistic concurrency token, incremented on every mutation
    public int Version { get; private set; }

    public IReadOnlyCollection<Attendee> Attendees => _attendees.AsReadOnly();

    // snapshot, not a live view - callers (e.g. the dispatcher) must see a stable list
    // even after ClearDomainEvents() runs later in the same unit of work
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.ToList();

    private Event()
    {
    }

    public static Event Create(string title, string description, TimeRange timeRange, IEnumerable<(string Name, EmailAddress Email, bool? IsAttending)> attendees)
    {
        var @event = new Event
        {
            Id = Guid.NewGuid(),
            Status = EventStatus.Scheduled,
            Version = 0
        };

        @event.SetTitle(title);
        @event.SetDescription(description);
        @event.TimeRange = timeRange;

        foreach (var attendee in attendees)
            @event._attendees.Add(new Attendee(attendee.Name, attendee.Email, attendee.IsAttending));

        @event.Raise(new EventCreated(@event.Id, @event.Title));
        return @event;
    }

    public void Update(string title, string description, TimeRange timeRange)
    {
        EnsureScheduled();
        SetTitle(title);
        SetDescription(description);
        TimeRange = timeRange;
        Raise(new EventUpdated(Id, Title));
    }

    public void Cancel()
    {
        if (Status == EventStatus.Cancelled)
            return;

        Status = EventStatus.Cancelled;
        Raise(new EventCancelled(Id, Title));
    }

    public Attendee AddAttendee(string name, EmailAddress email, bool? isAttending = null)
    {
        EnsureScheduled();
        var attendee = new Attendee(name, email, isAttending);
        _attendees.Add(attendee);
        Raise(new AttendeeAdded(Id, attendee.Id, email.Value));
        return attendee;
    }

    public void RespondAttendee(Guid attendeeId, bool isAttending)
    {
        EnsureScheduled();
        var attendee = _attendees.FirstOrDefault(a => a.Id == attendeeId)
            ?? throw new DomainException($"Attendee '{attendeeId}' was not found on this event.");

        attendee.Respond(isAttending);
        Raise(new AttendeeResponded(Id, attendeeId, isAttending));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    private void SetTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Title is required.");

        if (title.Length > MaxTitleLength)
            throw new DomainException($"Title must be {MaxTitleLength} characters or fewer.");

        Title = title;
    }

    private void SetDescription(string description)
    {
        if (description is not null && description.Length > MaxDescriptionLength)
            throw new DomainException($"Description must be {MaxDescriptionLength} characters or fewer.");

        Description = description ?? string.Empty;
    }

    private void EnsureScheduled()
    {
        if (Status == EventStatus.Cancelled)
            throw new DomainException("Cannot modify a cancelled event.");
    }

    private void Raise(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
        Version++;
    }
}
