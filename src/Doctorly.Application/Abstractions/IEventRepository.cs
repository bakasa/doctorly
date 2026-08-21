using Doctorly.Domain.Events;

namespace Doctorly.Application.Abstractions;

public sealed record EventFilter(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    EventStatus? Status = null,
    string? AttendeeEmail = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);

public sealed record EventPage<T>(IReadOnlyList<T> Items, int TotalCount);

public interface IEventRepository
{
    Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<EventPage<Event>> ListAsync(EventFilter filter, CancellationToken cancellationToken);

    Task AddAsync(Event @event, CancellationToken cancellationToken);
}
