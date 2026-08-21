using Doctorly.Application.Abstractions;
using Doctorly.Application.Events.Dtos;
using Doctorly.Application.Exceptions;
using Doctorly.Application.Notifications;
using Doctorly.Domain.Events;
using Doctorly.Domain.ValueObjects;

namespace Doctorly.Application.Events;

public sealed class EventsAppService(
    IEventRepository repository,
    IUnitOfWork unitOfWork,
    IDomainEventDispatcher domainEventDispatcher)
{
    public async Task<EventDto> CreateEventAsync(CreateEventRequest request, CancellationToken cancellationToken)
    {
        var attendees = request.Attendees.Select(a =>
            (a.Name, new EmailAddress(a.Email), a.IsAttending));

        var @event = Event.Create(
            request.Title,
            request.Description,
            new TimeRange(request.Start, request.End),
            attendees);

        await repository.AddAsync(@event, cancellationToken);
        await CommitAsync(@event, cancellationToken);

        return ToDto(@event);
    }

    public async Task<EventDto> GetEventAsync(Guid id, CancellationToken cancellationToken)
    {
        var @event = await GetOrThrowAsync(id, cancellationToken);
        return ToDto(@event);
    }

    public async Task<Abstractions.EventPage<EventDto>> ListEventsAsync(EventFilter filter, CancellationToken cancellationToken)
    {
        var page = await repository.ListAsync(filter, cancellationToken);
        return new Abstractions.EventPage<EventDto>(page.Items.Select(ToDto).ToList(), page.TotalCount);
    }

    public async Task<EventDto> UpdateEventAsync(Guid id, UpdateEventRequest request, int expectedVersion, CancellationToken cancellationToken)
    {
        var @event = await GetOrThrowAsync(id, cancellationToken);
        EnsureExpectedVersion(@event, expectedVersion);

        @event.Update(request.Title, request.Description, new TimeRange(request.Start, request.End));
        await CommitAsync(@event, cancellationToken);

        return ToDto(@event);
    }

    public async Task CancelEventAsync(Guid id, CancellationToken cancellationToken)
    {
        var @event = await GetOrThrowAsync(id, cancellationToken);
        @event.Cancel();
        await CommitAsync(@event, cancellationToken);
    }

    public async Task<EventDto> AddAttendeeAsync(Guid eventId, CreateAttendeeRequest request, CancellationToken cancellationToken)
    {
        var @event = await GetOrThrowAsync(eventId, cancellationToken);
        @event.AddAttendee(request.Name, new EmailAddress(request.Email), request.IsAttending);
        await CommitAsync(@event, cancellationToken);

        return ToDto(@event);
    }

    public async Task<EventDto> RespondAsync(Guid eventId, Guid attendeeId, bool isAttending, CancellationToken cancellationToken)
    {
        var @event = await GetOrThrowAsync(eventId, cancellationToken);
        @event.RespondAttendee(attendeeId, isAttending);
        await CommitAsync(@event, cancellationToken);

        return ToDto(@event);
    }

    private async Task<Event> GetOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Event '{id}' was not found.");
    }

    private static void EnsureExpectedVersion(Event @event, int expectedVersion)
    {
        if (@event.Version != expectedVersion)
            throw new ConcurrencyConflictException(
                $"Event '{@event.Id}' is at version {@event.Version}, but version {expectedVersion} was expected. Refetch and retry.");
    }

    private async Task CommitAsync(Event @event, CancellationToken cancellationToken)
    {
        await unitOfWork.CommitAsync(cancellationToken);
        await domainEventDispatcher.DispatchAsync(@event.DomainEvents, cancellationToken);
        @event.ClearDomainEvents();
    }

    private static EventDto ToDto(Event @event) => new(
        @event.Id,
        @event.Title,
        @event.Description,
        @event.TimeRange.Start,
        @event.TimeRange.End,
        @event.Status,
        @event.Version,
        @event.Attendees.Select(a => new AttendeeDto(a.Id, a.Name, a.Email.Value, a.IsAttending)).ToList());
}
