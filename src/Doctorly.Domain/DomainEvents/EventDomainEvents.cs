namespace Doctorly.Domain.DomainEvents;

public sealed record EventCreated(Guid EventId, string Title) : IDomainEvent;

public sealed record EventUpdated(Guid EventId, string Title) : IDomainEvent;

public sealed record EventCancelled(Guid EventId, string Title) : IDomainEvent;

public sealed record AttendeeAdded(Guid EventId, Guid AttendeeId, string Email) : IDomainEvent;

public sealed record AttendeeResponded(Guid EventId, Guid AttendeeId, bool IsAttending) : IDomainEvent;
