using Doctorly.Domain.Events;

namespace Doctorly.Application.Events.Dtos;

public sealed record EventDto(
    Guid Id,
    string Title,
    string Description,
    DateTimeOffset Start,
    DateTimeOffset End,
    EventStatus Status,
    int Version,
    IReadOnlyList<AttendeeDto> Attendees);

public sealed record CreateEventRequest(
    string Title,
    string Description,
    DateTimeOffset Start,
    DateTimeOffset End,
    IReadOnlyList<CreateAttendeeRequest> Attendees);

public sealed record UpdateEventRequest(
    string Title,
    string Description,
    DateTimeOffset Start,
    DateTimeOffset End);
